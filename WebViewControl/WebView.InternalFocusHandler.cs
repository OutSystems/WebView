using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalFocusHandler : FocusHandler {

            private WebView OwnerWebView { get; }

            public InternalFocusHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override void OnGotFocus(CefBrowser browser) {
                OwnerWebView.OnGotFocus();
            }

            protected override bool OnSetFocus(CefBrowser browser, CefFocusSource source) {
                return OwnerWebView.OnSetFocus(source == CefFocusSource.System);
            }

            protected override void OnTakeFocus(CefBrowser browser, bool next) {
                OwnerWebView.OnLostFocus();
            }
        }
    }
}
