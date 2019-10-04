using Xilium.CefGlue;

namespace WebViewControl {

    internal partial class ChromiumBrowser {

        internal void CreateBrowser() {
            CreateOrUpdateBrowser(200, 200);
        }

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
