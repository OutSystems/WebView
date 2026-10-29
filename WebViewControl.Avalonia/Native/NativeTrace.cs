using System;
using System.Threading;
using Avalonia.Threading;

namespace WebViewControl.Native {

    /// <summary>
    /// Set the WEBVIEW_NATIVE_TRACE environment variable to 1 to trace the native bridge activity (written to the standard error).
    /// </summary>
    internal static class NativeTrace {

        public static bool Enabled { get; } = Environment.GetEnvironmentVariable("WEBVIEW_NATIVE_TRACE") == "1";

        public static void Write(string message) {
            if (Enabled) {
                var thread = Thread.CurrentThread;
                var where = Dispatcher.UIThread.CheckAccess() ? "UI" : $"thread {thread.ManagedThreadId}";
                Console.Error.WriteLine($"[webview-native] [{where}] {message}");
            }
        }
    }
}
