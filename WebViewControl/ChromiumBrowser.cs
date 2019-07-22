using Xilium.CefGlue;
using Xilium.CefGlue.WPF;

namespace WebViewControl {

    internal class ChromiumBrowser : WpfCefBrowser {

        internal void CreateBrowser() {
            CreateOrUpdateBrowser(200, 200);
        }

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
