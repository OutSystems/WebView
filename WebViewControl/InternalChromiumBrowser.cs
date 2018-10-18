using System.Windows;
using System.Windows.Input;
using CefSharp.Wpf;

namespace WebViewControl {

    internal class InternalChromiumBrowser : ChromiumWebBrowser {

        public InternalChromiumBrowser(bool preloadBrowser) {
            if (preloadBrowser) {
                // create internal browser to speed up load
                CreateOffscreenBrowser(new Size(200, 200));
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            e.Handled = false; // let the mouse event be fired
        }
    }
}
