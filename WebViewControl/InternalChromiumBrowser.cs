using System.Windows;
using System.Windows.Input;
using CefSharp.Wpf;

namespace WebViewControl {

    internal class InternalChromiumBrowser : ChromiumWebBrowser {

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            e.Handled = false; // let the mouse event be fired
        }

        internal void CreateBrowser() {
            CreateOffscreenBrowser(new Size(200, 200));
        }
    }
}
