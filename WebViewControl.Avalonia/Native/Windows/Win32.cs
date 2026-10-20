using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WebViewControl {

    /// <summary>
    /// Win32 interop to host WebView2: each web view renders in a child window of its own, which Avalonia moves into its
    /// window when the control is attached (and parks off screen when detached).
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static unsafe class Win32 {

        public const uint WM_SIZE = 0x0005;
        public const uint WM_MOVE = 0x0003;
        public const uint WM_SETFOCUS = 0x0007;
        public const uint WM_WINDOWPOSCHANGED = 0x0047;

        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CLIPCHILDREN = 0x02000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_POPUP = 0x80000000;

        private const string WindowClassName = "WebViewControlWebView2Host";

        private static readonly ConcurrentDictionary<IntPtr, Action<uint>> MessageHandlers = new();
        private static readonly Lazy<IntPtr> ParkingWindow = new(CreateParkingWindow);
        private static readonly Lazy<ushort> WindowClass = new(RegisterWindowClass);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASSEX {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public IntPtr lpszMenuName;
            public IntPtr lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern ushort RegisterClassExW(ref WNDCLASSEX windowClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(uint exStyle, IntPtr className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr window, out RECT rect);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        /// <summary>
        /// Creates a child window (parked in a hidden window until Avalonia attaches it) that forwards its messages to the handler.
        /// </summary>
        public static IntPtr CreateChildWindow(int width, int height, Action<uint> onMessage) {
            var window = CreateWindowExW(0, (IntPtr)WindowClass.Value, string.Empty, WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS, 0, 0, width, height, ParkingWindow.Value, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (window == IntPtr.Zero) {
                throw new InvalidOperationException($"Unable to create the web view window (error {Marshal.GetLastWin32Error()})");
            }
            MessageHandlers[window] = onMessage;
            return window;
        }

        public static void DestroyChildWindow(IntPtr window) {
            MessageHandlers.TryRemove(window, out _);
            DestroyWindow(window);
        }

        private static ushort RegisterWindowClass() {
            var className = Marshal.StringToHGlobalUni(WindowClassName); // kept for the lifetime of the process
            var windowClass = new WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, IntPtr>)&WindowProcedure,
                hInstance = GetModuleHandleW(null),
                lpszClassName = className
            };
            var atom = RegisterClassExW(ref windowClass);
            if (atom == 0) {
                throw new InvalidOperationException($"Unable to register the web view window class (error {Marshal.GetLastWin32Error()})");
            }
            return atom;
        }

        private static IntPtr CreateParkingWindow() {
            var window = CreateWindowExW(0, (IntPtr)WindowClass.Value, string.Empty, WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (window == IntPtr.Zero) {
                throw new InvalidOperationException($"Unable to create the web view parking window (error {Marshal.GetLastWin32Error()})");
            }
            return window;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) {
            if (MessageHandlers.TryGetValue(window, out var handler)) {
                try {
                    handler(message);
                } catch (Exception e) {
                    // exceptions must not cross back into native code
                    System.Diagnostics.Trace.TraceError("WebView: {0}", e);
                }
            }
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }
}
