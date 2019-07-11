using Xilium.CefGlue;

namespace WebViewControl {

    partial class WebView {

        private class CefMenuHandler : Xilium.CefGlue.Common.Handlers.ContextMenuHandler {

            private WebView OwnerWebView { get; }

            public CefMenuHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override void OnBeforeContextMenu(CefBrowser browser, CefFrame frame, CefContextMenuParams state, CefMenuModel model) {
                if (OwnerWebView.DisableBuiltinContextMenus) {
                    model.Clear();
                }
            }
        }
    }
}
