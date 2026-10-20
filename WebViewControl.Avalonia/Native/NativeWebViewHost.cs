using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace WebViewControl {

    /// <summary>
    /// Hosts the native view of a <see cref="NativeWebViewEngine"/> (NSView on macOS, HWND on Windows) in the Avalonia visual tree.
    /// </summary>
    internal sealed class NativeWebViewHost : NativeControlHost {

        private readonly NativeWebViewEngine engine;

        public NativeWebViewHost(NativeWebViewEngine engine) {
            this.engine = engine;
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent) {
            var size = Bounds.Size;
            var handle = engine.GetOrCreateNativeView((int)size.Width, (int)size.Height);
            // the view may have been created (eg: preloaded) with another size or hosted elsewhere before
            engine.SetSize(size.Width, size.Height);
            return new PlatformHandle(handle, engine.NativeViewHandleDescriptor);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            engine.SetSize(finalSize.Width, finalSize.Height);
            return base.ArrangeOverride(finalSize);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control) {
            // the native view belongs to the engine: it's reused when the control is attached again (eg: when switching tabs)
            // and destroyed when the engine is disposed
        }
    }
}
