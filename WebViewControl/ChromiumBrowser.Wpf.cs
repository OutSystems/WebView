using Xilium.CefGlue.WPF;

namespace WebViewControl {

    partial class ChromiumBrowser : WpfCefBrowser {

        public new void CreateBrowser(int width, int height) {
            base.CreateBrowser(width, height);
        }
    }
}
