using Xilium.CefGlue;

namespace WebViewControl {

    internal partial class ChromiumBrowser {

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
