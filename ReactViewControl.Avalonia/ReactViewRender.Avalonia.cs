using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender : ContentControl, IStyleable {

        private static Window hiddenWindow;

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            if (hiddenWindow == null) {
                hiddenWindow = new Window() {
                    IsVisible = false,
                    Focusable = false,
                    Title = "Hidden React View Window"
                };
            }

            WebView.HostingWindow = hiddenWindow;
            WebView.AllowNativeMethodsParallelExecution = !ForceNativeSyncCalls;
            Content = WebView;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            if (!WebView.OsrEnabled && e.Property == IsEffectivelyEnabledProperty) {
                DisableInteractions(!IsEffectivelyEnabled);
            }
        }
    }
}
