using Xilium.CefGlue;

namespace WebViewControl {

    internal partial class ChromiumBrowser {

        internal void CreateBrowser() {
            CreateBrowser(1, 1);
        }

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
