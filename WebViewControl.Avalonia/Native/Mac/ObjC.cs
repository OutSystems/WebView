using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WebViewControl {

    /// <summary>
    /// Minimal Objective-C runtime interop: enough to drive WKWebView without a binding library.
    /// Every method must be called on the main thread, as AppKit and WebKit require.
    /// Methods named Create* return retained (+1) objects that must be released by the caller.
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal static unsafe class ObjC {

        private const string LibObjC = "/usr/lib/libobjc.A.dylib";
        private const string WebKitFramework = "/System/Library/Frameworks/WebKit.framework/WebKit";

        // offset of the invoke function pointer in a block literal: isa (8) + flags (4) + reserved (4)
        private const int BlockInvokeOffset = 16;

        private static readonly IntPtr msgSend = NativeLibrary.GetExport(NativeLibrary.Load(LibObjC), "objc_msgSend");
        private static readonly ConcurrentDictionary<string, IntPtr> classes = new();
        private static readonly ConcurrentDictionary<string, IntPtr> selectors = new();
        private static readonly Lazy<IntPtr> webKit = new(() => NativeLibrary.Load(WebKitFramework));

        [DllImport(LibObjC)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(LibObjC)]
        private static extern IntPtr objc_getProtocol(string name);

        [DllImport(LibObjC)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(LibObjC)]
        private static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, nint extraBytes);

        [DllImport(LibObjC)]
        private static extern void objc_registerClassPair(IntPtr cls);

        [DllImport(LibObjC)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool class_addMethod(IntPtr cls, IntPtr name, IntPtr implementation, string types);

        [DllImport(LibObjC)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool class_addProtocol(IntPtr cls, IntPtr protocol);

        [DllImport(LibObjC)]
        private static extern IntPtr objc_autoreleasePoolPush();

        [DllImport(LibObjC)]
        private static extern void objc_autoreleasePoolPop(IntPtr pool);

        public static void LoadWebKit() => _ = webKit.Value;

        public static IntPtr Class(string name) => classes.GetOrAdd(name, objc_getClass);

        public static IntPtr Selector(string name) => selectors.GetOrAdd(name, sel_registerName);

        /// <summary>
        /// Registers a new subclass with the given protocols and method implementations.
        /// </summary>
        public static IntPtr RegisterClass(string name, string superclassName, string[] protocols, params (string selector, IntPtr implementation, string types)[] methods) {
            var cls = objc_allocateClassPair(Class(superclassName), name, 0);
            if (cls == IntPtr.Zero) {
                throw new InvalidOperationException($"Unable to create the Objective-C class '{name}'");
            }
            foreach (var protocolName in protocols) {
                var protocol = objc_getProtocol(protocolName);
                if (protocol != IntPtr.Zero) {
                    class_addProtocol(cls, protocol);
                }
            }
            foreach (var (selector, implementation, types) in methods) {
                class_addMethod(cls, Selector(selector), implementation, types);
            }
            objc_registerClassPair(cls);
            return cls;
        }

        public static IntPtr Send(IntPtr receiver, string selector) {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)msgSend)(receiver, Selector(selector));
        }

        public static IntPtr Send(IntPtr receiver, string selector, IntPtr arg) {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)msgSend)(receiver, Selector(selector), arg);
        }

        public static IntPtr Send(IntPtr receiver, string selector, IntPtr arg1, IntPtr arg2) {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)msgSend)(receiver, Selector(selector), arg1, arg2);
        }

        public static void SendInteger(IntPtr receiver, string selector, nint value) {
            ((delegate* unmanaged<IntPtr, IntPtr, nint, void>)msgSend)(receiver, Selector(selector), value);
        }

        public static void SendBool(IntPtr receiver, string selector, bool value) {
            ((delegate* unmanaged<IntPtr, IntPtr, byte, void>)msgSend)(receiver, Selector(selector), value ? (byte)1 : (byte)0);
        }

        public static void SendRect(IntPtr receiver, string selector, CGRect value) {
            ((delegate* unmanaged<IntPtr, IntPtr, CGRect, void>)msgSend)(receiver, Selector(selector), value);
        }

        public static void SendDouble(IntPtr receiver, string selector, double value) {
            ((delegate* unmanaged<IntPtr, IntPtr, double, void>)msgSend)(receiver, Selector(selector), value);
        }

        public static bool GetBool(IntPtr receiver, string selector) {
            return ((delegate* unmanaged<IntPtr, IntPtr, byte>)msgSend)(receiver, Selector(selector)) != 0;
        }

        public static bool GetBool(IntPtr receiver, string selector, IntPtr arg) {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)msgSend)(receiver, Selector(selector), arg) != 0;
        }

        public static nint GetInteger(IntPtr receiver, string selector) {
            return ((delegate* unmanaged<IntPtr, IntPtr, nint>)msgSend)(receiver, Selector(selector));
        }

        public static bool RespondsTo(IntPtr receiver, string selector) {
            return receiver != IntPtr.Zero && GetBool(receiver, "respondsToSelector:", Selector(selector));
        }

        public static bool IsKindOf(IntPtr instance, string className) {
            return instance != IntPtr.Zero && GetBool(instance, "isKindOfClass:", Class(className));
        }

        public static IntPtr New(string className) => New(Class(className));

        public static IntPtr New(IntPtr cls) => Send(Send(cls, "alloc"), "init");

        public static IntPtr Retain(IntPtr instance) => instance == IntPtr.Zero ? IntPtr.Zero : Send(instance, "retain");

        public static void Release(IntPtr instance) {
            if (instance != IntPtr.Zero) {
                Send(instance, "release");
            }
        }

        public static IntPtr CreateString(string value) {
            value ??= string.Empty;
            fixed (char* chars = value) {
                var instance = Send(Class("NSString"), "alloc");
                return ((delegate* unmanaged<IntPtr, IntPtr, char*, nuint, IntPtr>)msgSend)(instance, Selector("initWithCharacters:length:"), chars, (nuint)value.Length);
            }
        }

        /// <summary>
        /// Reads an NSString (returns null for nil or for objects that are not strings).
        /// </summary>
        public static string GetString(IntPtr nsString) {
            if (!IsKindOf(nsString, "NSString")) {
                return null;
            }
            var utf8 = Send(nsString, "UTF8String");
            return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
        }

        /// <summary>
        /// Reads the absolute string of an NSURL.
        /// </summary>
        public static string GetUrlString(IntPtr nsUrl) => nsUrl == IntPtr.Zero ? null : GetString(Send(nsUrl, "absoluteString"));

        public static IntPtr CreateUrl(string url) {
            var urlString = CreateString(url);
            try {
                return Retain(Send(Class("NSURL"), "URLWithString:", urlString));
            } finally {
                Release(urlString);
            }
        }

        public static IntPtr CreateData(byte[] bytes) {
            fixed (byte* pointer = bytes) {
                var instance = Send(Class("NSData"), "alloc");
                return ((delegate* unmanaged<IntPtr, IntPtr, byte*, nuint, IntPtr>)msgSend)(instance, Selector("initWithBytes:length:"), pointer, (nuint)bytes.Length);
            }
        }

        public static IntPtr CreateDictionary(NameValueCollection values) {
            var dictionary = New("NSMutableDictionary");
            foreach (string key in values) {
                var nsKey = CreateString(key);
                var nsValue = CreateString(values[key]);
                Send(dictionary, "setObject:forKey:", nsValue, nsKey);
                Release(nsKey);
                Release(nsValue);
            }
            return dictionary;
        }

        /// <summary>
        /// Reads an NSDictionary of strings (eg: http headers).
        /// </summary>
        public static NameValueCollection GetStringDictionary(IntPtr dictionary) {
            var result = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (!IsKindOf(dictionary, "NSDictionary")) {
                return result;
            }
            var keys = Send(dictionary, "allKeys");
            var count = GetInteger(keys, "count");
            for (nint i = 0; i < count; i++) {
                var key = ((delegate* unmanaged<IntPtr, IntPtr, nuint, IntPtr>)msgSend)(keys, Selector("objectAtIndex:"), (nuint)i);
                var name = GetString(key);
                if (name != null) {
                    result[name] = GetString(Send(dictionary, "objectForKey:", key));
                }
            }
            return result;
        }

        public static IntPtr CreateUserScript(IntPtr source, nint injectionTime, bool forMainFrameOnly) {
            var instance = Send(Class("WKUserScript"), "alloc");
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, nint, byte, IntPtr>)msgSend)(instance, Selector("initWithSource:injectionTime:forMainFrameOnly:"), source, injectionTime, forMainFrameOnly ? (byte)1 : (byte)0);
        }

        public static IntPtr CreateWebView(CGRect frame, IntPtr configuration) {
            var instance = Send(Class("WKWebView"), "alloc");
            return ((delegate* unmanaged<IntPtr, IntPtr, CGRect, IntPtr, IntPtr>)msgSend)(instance, Selector("initWithFrame:configuration:"), frame, configuration);
        }

        public static IntPtr CreateHttpResponse(IntPtr url, nint statusCode, IntPtr httpVersion, IntPtr headerFields) {
            var instance = Send(Class("NSHTTPURLResponse"), "alloc");
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, nint, IntPtr, IntPtr, IntPtr>)msgSend)(instance, Selector("initWithURL:statusCode:HTTPVersion:headerFields:"), url, statusCode, httpVersion, headerFields);
        }

        public static void AddObserver(IntPtr target, IntPtr observer, string keyPath, nuint options) {
            var nsKeyPath = CreateString(keyPath);
            ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, nuint, IntPtr, void>)msgSend)(target, Selector("addObserver:forKeyPath:options:context:"), observer, nsKeyPath, options, IntPtr.Zero);
            Release(nsKeyPath);
        }

        public static void RemoveObserver(IntPtr target, IntPtr observer, string keyPath) {
            var nsKeyPath = CreateString(keyPath);
            Send(target, "removeObserver:forKeyPath:", observer, nsKeyPath);
            Release(nsKeyPath);
        }

        /// <summary>
        /// Calls a block that takes an NSInteger (eg: a WKNavigationActionPolicy decision handler).
        /// </summary>
        public static void InvokeBlock(IntPtr block, nint argument) {
            var invoke = *(IntPtr*)((byte*)block + BlockInvokeOffset);
            ((delegate* unmanaged<IntPtr, nint, void>)invoke)(block, argument);
        }

        public static AutoreleasePool CreateAutoreleasePool() => new(objc_autoreleasePoolPush());

        public readonly struct AutoreleasePool : IDisposable {

            private readonly IntPtr pool;

            public AutoreleasePool(IntPtr pool) {
                this.pool = pool;
            }

            public void Dispose() => objc_autoreleasePoolPop(pool);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGRect {

        public CGRect(double x, double y, double width, double height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X;
        public double Y;
        public double Width;
        public double Height;
    }
}
