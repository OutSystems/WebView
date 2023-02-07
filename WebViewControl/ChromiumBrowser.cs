using Xilium.CefGlue;
using Xilium.CefGlue.Avalonia;

namespace WebViewControl {

    class ChromiumBrowser : AvaloniaCefBrowser {
        internal CefBrowser GetBrowser() => UnderlyingBrowser;

        public new void CreateBrowser(int width, int height) {
            if (IsBrowserInitialized) {
                return;
            }
            base.CreateBrowser(width, height);
        }
    }
}
