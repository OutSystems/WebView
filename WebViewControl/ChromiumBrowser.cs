using Xilium.CefGlue;

namespace WebViewControl {

    internal partial class ChromiumBrowser {

        internal void CreateBrowser() {
            CreateOrUpdateBrowser(1, 1);
        }

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
