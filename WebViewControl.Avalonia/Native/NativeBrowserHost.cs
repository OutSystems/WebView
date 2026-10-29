using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace WebViewControl.Native {

    /// <summary>
    /// Avalonia control that hosts the native web view (as a child of the window's native view).
    /// </summary>
    internal sealed class NativeBrowserHost : NativeControlHost {

        private readonly WebView owner;
        private readonly INativeBrowser browser;

        public NativeBrowserHost(WebView owner, INativeBrowser browser) {
            this.owner = owner;
            this.browser = browser;
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent) {
            // the browser might have been created already (preloaded views), in that case the existing view is just attached
            owner.EnsureBrowserCreated(800, 600);
            browser.OnHostBoundsChanged(Bounds.Width, Bounds.Height, this.GetVisualRoot()?.RenderScaling ?? 1.0);
            return browser.CreatePlatformHandle(parent);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty) {
                browser.OnHostBoundsChanged(Bounds.Width, Bounds.Height, this.GetVisualRoot()?.RenderScaling ?? 1.0);
            }
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control) {
            // the native view is owned by the browser, it can be attached again later. It is released when the web view is disposed.
            browser.OnHostDetached();
        }
    }
}
