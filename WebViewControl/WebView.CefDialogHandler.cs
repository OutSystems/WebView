using Xilium.CefGlue;

namespace WebViewControl
{

    partial class WebView {
        private class CefDialogHandler : Xilium.CefGlue.Common.Handlers.DialogHandler {

            private WebView OwnerWebView { get; }

            public CefDialogHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnFileDialog(CefBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, string[] acceptFilters, int selectedAcceptFilter, CefFileDialogCallback callback) {
                if (OwnerWebView.DisableFileDialogs) {
                    return true;
                }
                return false;
            }
        }
    }
}
