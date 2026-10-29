using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text;

namespace WebViewControl.Native.Mac {

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGRect {
        public double X, Y, Width, Height;

        public CGRect(double x, double y, double width, double height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"({X},{Y},{Width}x{Height})";
    }

    /// <summary>
    /// Minimal Objective-C runtime interop. Everything here must be called from the main (UI) thread.
    /// </summary>
    internal static unsafe class Objc {

        private const string LibObjc = "/usr/lib/libobjc.A.dylib";
        private const string LibSystem = "/usr/lib/libSystem.dylib";

        public const long NSUTF8StringEncoding = 4;

        [DllImport(LibObjc)] public static extern IntPtr objc_getClass(string name);
        [DllImport(LibObjc)] public static extern IntPtr objc_getProtocol(string name);
        [DllImport(LibObjc)] public static extern IntPtr sel_registerName(string name);
        [DllImport(LibObjc)] public static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, UIntPtr extraBytes);
        [DllImport(LibObjc)] public static extern void objc_registerClassPair(IntPtr cls);
        [DllImport(LibObjc)] public static extern byte class_addMethod(IntPtr cls, IntPtr selector, IntPtr implementation, string types);
        [DllImport(LibObjc)] public static extern byte class_addProtocol(IntPtr cls, IntPtr protocol);

        [DllImport(LibSystem)] public static extern IntPtr dlopen(string path, int mode);
        [DllImport(LibSystem)] public static extern IntPtr dlsym(IntPtr handle, string symbol);
        [DllImport(LibSystem, EntryPoint = "_Block_copy")] public static extern IntPtr Block_copy(IntPtr block);
        [DllImport(LibSystem, EntryPoint = "_Block_release")] public static extern void Block_release(IntPtr block);

        // ---- objc_msgSend flavours (one overload per signature we need) ----------------------------------------------

        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, long b, IntPtr c, IntPtr d);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, long c);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, long a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, byte a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, double a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, double a, double b, double c, double d);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, long b, byte c);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr bytes, UIntPtr length, long encoding);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, CGRect rect);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, CGRect rect, IntPtr a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, CGRect rect, long a, long b, byte c);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, long a, IntPtr b, IntPtr c, byte d);

        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern byte SendBool(IntPtr receiver, IntPtr selector);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern byte SendBool(IntPtr receiver, IntPtr selector, IntPtr a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern long SendLong(IntPtr receiver, IntPtr selector);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern long SendLong(IntPtr receiver, IntPtr selector, long a);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] public static extern double SendDouble(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjc, EntryPoint = "objc_msgSend")] private static extern CGRect SendRectArm64(IntPtr receiver, IntPtr selector);
        [DllImport(LibObjc, EntryPoint = "objc_msgSend_stret")] private static extern void SendRectX64(out CGRect result, IntPtr receiver, IntPtr selector);

        public static CGRect SendRect(IntPtr receiver, IntPtr selector) {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64) {
                // structs larger than 16 bytes are returned through a hidden pointer on x86_64
                SendRectX64(out var result, receiver, selector);
                return result;
            }
            return SendRectArm64(receiver, selector);
        }

        // ---- helpers -------------------------------------------------------------------------------------------------

        public static IntPtr Sel(string selector) => sel_registerName(selector);

        public static IntPtr Cls(string className) => objc_getClass(className);

        public static bool RespondsTo(IntPtr obj, string selector) => obj != IntPtr.Zero && SendBool(obj, Sel("respondsToSelector:"), Sel(selector)) != 0;

        public static IntPtr Retain(IntPtr obj) => obj == IntPtr.Zero ? obj : Send(obj, Sel("retain"));

        public static void Release(IntPtr obj) {
            if (obj != IntPtr.Zero) {
                Send(obj, Sel("release"));
            }
        }

        public static IntPtr New(string className) => Send(Send(Cls(className), Sel("alloc")), Sel("init"));

        /// <summary>
        /// Creates an (autoreleased) NSString.
        /// </summary>
        public static IntPtr NSString(string value) {
            if (value == null) {
                return IntPtr.Zero;
            }
            var bytes = Encoding.UTF8.GetBytes(value);
            fixed (byte* p = bytes) {
                var str = Send(Send(Cls("NSString"), Sel("alloc")), Sel("initWithBytes:length:encoding:"), (IntPtr)p, (UIntPtr)bytes.Length, NSUTF8StringEncoding);
                return Send(str, Sel("autorelease"));
            }
        }

        public static string FromNSString(IntPtr nsString) {
            if (nsString == IntPtr.Zero) {
                return null;
            }
            var utf8 = Send(nsString, Sel("UTF8String"));
            if (utf8 == IntPtr.Zero) {
                return null;
            }
            var length = SendLong(nsString, Sel("lengthOfBytesUsingEncoding:"), NSUTF8StringEncoding);
            return Marshal.PtrToStringUTF8(utf8, (int)length);
        }

        /// <summary>
        /// Creates an (autoreleased) NSData, copying the bytes.
        /// </summary>
        public static IntPtr NSData(byte[] bytes) {
            if (bytes == null || bytes.Length == 0) {
                return Send(Cls("NSData"), Sel("data"));
            }
            fixed (byte* p = bytes) {
                return Send(Cls("NSData"), Sel("dataWithBytes:length:"), (IntPtr)p, (IntPtr)bytes.Length);
            }
        }

        public static IntPtr NSUrl(string url) => url == null ? IntPtr.Zero : Send(Cls("NSURL"), Sel("URLWithString:"), NSString(url));

        public static string FromNSUrl(IntPtr nsUrl) => nsUrl == IntPtr.Zero ? null : FromNSString(Send(nsUrl, Sel("absoluteString")));

        public static IntPtr NSNumber(bool value) => Send(Cls("NSNumber"), Sel("numberWithBool:"), (byte)(value ? 1 : 0));

        public static NameValueCollection FromNSDictionary(IntPtr dictionary) {
            var result = new NameValueCollection();
            if (dictionary == IntPtr.Zero) {
                return result;
            }
            var keys = Send(dictionary, Sel("allKeys"));
            var count = SendLong(keys, Sel("count"));
            for (long i = 0; i < count; i++) {
                var key = Send(keys, Sel("objectAtIndex:"), (IntPtr)i);
                var value = Send(dictionary, Sel("objectForKey:"), key);
                result[FromNSString(key)] = FromNSString(value);
            }
            return result;
        }

        public static IntPtr ToNSDictionary(NameValueCollection values) {
            var dictionary = Send(Cls("NSMutableDictionary"), Sel("dictionary"));
            if (values != null) {
                foreach (var key in values.AllKeys) {
                    Send(dictionary, Sel("setObject:forKey:"), NSString(values[key]), NSString(key));
                }
            }
            return dictionary;
        }
    }

    /// <summary>
    /// Calls the objective-c blocks received as arguments (completion handlers).
    /// </summary>
    internal static unsafe class Block {

        // struct Block_literal { void* isa; int flags; int reserved; void (*invoke)(...); ... }
        private const int InvokeOffset = 16;

        private static IntPtr InvokePointer(IntPtr block) => *(IntPtr*)((byte*)block + InvokeOffset);

        public static void Call(IntPtr block) => ((delegate* unmanaged[Cdecl]<IntPtr, void>)InvokePointer(block))(block);

        public static void Call(IntPtr block, long argument) => ((delegate* unmanaged[Cdecl]<IntPtr, long, void>)InvokePointer(block))(block, argument);

        public static void Call(IntPtr block, IntPtr argument) => ((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)InvokePointer(block))(block, argument);

        public static void Call(IntPtr block, byte argument) => ((delegate* unmanaged[Cdecl]<IntPtr, byte, void>)InvokePointer(block))(block, argument);

        public static void Call(IntPtr block, long argument1, IntPtr argument2) => ((delegate* unmanaged[Cdecl]<IntPtr, long, IntPtr, void>)InvokePointer(block))(block, argument1, argument2);
    }

    /// <summary>
    /// Registers a new objective-c class at runtime, whose methods are implemented by static [UnmanagedCallersOnly] methods.
    /// </summary>
    internal sealed class ObjcClassBuilder {

        private readonly IntPtr cls;
        private readonly string name;

        public ObjcClassBuilder(string name, string superclass = "NSObject") {
            this.name = name;
            cls = Objc.objc_allocateClassPair(Objc.Cls(superclass), name, UIntPtr.Zero);
            if (cls == IntPtr.Zero) {
                throw new InvalidOperationException($"Unable to create objective-c class {name}");
            }
        }

        public unsafe ObjcClassBuilder AddMethod(string selector, void* implementation, string typeEncoding) {
            if (Objc.class_addMethod(cls, Objc.Sel(selector), (IntPtr)implementation, typeEncoding) == 0) {
                throw new InvalidOperationException($"Unable to add method {selector} to {name}");
            }
            return this;
        }

        public ObjcClassBuilder AddProtocol(string protocolName) {
            var protocol = Objc.objc_getProtocol(protocolName);
            if (protocol != IntPtr.Zero) {
                Objc.class_addProtocol(cls, protocol);
            }
            return this;
        }

        public IntPtr Register() {
            Objc.objc_registerClassPair(cls);
            return cls;
        }
    }
}
