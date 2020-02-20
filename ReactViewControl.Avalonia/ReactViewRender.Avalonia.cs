using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
            Content = WebView;
        }

        partial void ShowWindow(Action close) {
            System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ => close());
            //Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            //    var window = new Window();
            //    window.Closed += delegate { close(); };
            //    window.Show(); // ((IClassicDesktopStyleApplicationLifetime) Application.Current.ApplicationLifetime).Windows.First());
            //});
        }
    }
}
