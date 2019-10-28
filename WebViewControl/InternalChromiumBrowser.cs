using System;
using System.Windows.Input;
using CefSharp.Wpf;

namespace WebViewControl {

    internal class InternalChromiumBrowser : ChromiumWebBrowser {

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            e.Handled = false; // let the mouse event be fired
        }

        internal void CreateBrowser() {
            CreateOffscreenBrowser(new System.Windows.Size(200, 200));
        }

        protected override CefSharp.Structs.Rect GetViewRect() {
            // prevent returning negative values that will cause cef crash https://github.com/cefsharp/CefSharp/pull/2879#pullrequestreview-295628479
            var rect = base.GetViewRect();
            return new CefSharp.Structs.Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
        }
    }
}
