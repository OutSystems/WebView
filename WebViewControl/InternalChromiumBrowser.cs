using System.Windows.Input;
using CefSharp.Wpf;

namespace WebViewControl {

    public class InternalChromiumBrowser : ChromiumWebBrowser {

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            e.Handled = false; // let the mouse event be fired
        }
    }
}
