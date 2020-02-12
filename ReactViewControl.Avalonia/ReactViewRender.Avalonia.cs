using System;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;

namespace ReactViewControl {

    partial class ReactViewRender : ContentControl, IStyleable {

        private static Window hiddenWindow;

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            Content = WebView;
        }

        private IntPtr GetHostViewHandle() {
            if (hiddenWindow == null) {
                hiddenWindow = new Window() {
                    IsVisible = false,
                    Focusable = false,
                    Title = "Hidden React View Window"
                };
            }
         
            var windowHandle = hiddenWindow.PlatformImpl.Handle;

            if (windowHandle is IMacOSTopLevelPlatformHandle macOSHandle) {
                return macOSHandle.GetNSViewRetained();
            }

            return windowHandle.Handle;
        }
    }
}
