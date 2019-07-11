using System.Windows;
using System.Windows.Input;
using Xilium.CefGlue.WPF;

namespace WebViewControl {

    internal class InternalChromiumBrowser : WpfCefBrowser {

        // TODO
        //protected override void OnMouseUp(MouseButtonEventArgs e) {
        //    base.OnMouseUp(e);
        //    e.Handled = false; // let the mouse event be fired
        //}

        internal void CreateBrowser() {
            // TODO CreateOffscreenBrowser(new Size(200, 200));
        }
    }
}
