using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender : ContentControl, IStyleable {

        private static Window hiddenWindow;

        Type IStyleable.StyleKey => typeof(ContentControl);

        private Window GetHiddenWindow() {
            if (hiddenWindow == null) {
                hiddenWindow = new Window() {
                    IsVisible = false,
                    Focusable = false,
                    Title = "Hidden React View Window"
                };
            }
            return hiddenWindow;
        }

        partial void ExtraInitialize() {
            Content = WebView;
        }

        partial void PreloadWebView() {
            var window = GetHiddenWindow();
            // initialize browser with full screen size to avoid html measure issues on initial render
            var initialBrowserSizeWidth = (int)window.Screens.All.Max(s => s.WorkingArea.Width * (WebView.OsrEnabled ? 1 : s.PixelDensity));
            var initialBrowserSizeHeight = (int)window.Screens.All.Max(s => s.WorkingArea.Height * (WebView.OsrEnabled ? 1 : s.PixelDensity));
            WebView.InitializeBrowser(window, initialBrowserSizeWidth, initialBrowserSizeHeight);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            if (!WebView.OsrEnabled && e.Property == IsEffectivelyEnabledProperty) {
                 DisableInputInteractions(!IsEffectivelyEnabled);
            }
        }
    }
}
