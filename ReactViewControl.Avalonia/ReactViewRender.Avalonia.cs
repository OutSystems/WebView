using System;
using Avalonia.Controls;
using Avalonia.Styling;

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
            WebView.AllowNativeMethodsParallelExecution = !SyncNativeCalls;
            Content = WebView;
        }
    }
}
