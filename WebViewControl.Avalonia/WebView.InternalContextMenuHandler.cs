using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {
        
        private class InternalContextMenuHandler : ContextMenuHandler {

            private WebView OwnerWebView { get; }

            public InternalContextMenuHandler(WebView webView) {
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
