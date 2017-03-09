using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private class MenuHandler : IContextMenuHandler {

            private readonly WebView OwnerWebView;

            public MenuHandler(WebView webView) {
                OwnerWebView = webView;
            }

            public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model) {
                if (OwnerWebView.DisableBuiltinContextMenus) {
                    model.Clear();
                }
            }

            public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags) {
                return false;
            }

            public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame) { }

            public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback) {
                return false;
            }
        }
    }
}
