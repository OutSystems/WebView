using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {
        private class InternalDialogHandler : DialogHandler {

            private WebView OwnerWebView { get; }

            public InternalDialogHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnFileDialog(CefBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, string[] acceptFilters, CefFileDialogCallback callback) {
                if (OwnerWebView.DisableFileDialogs) {
                    return true;
                }
                return false;
            }
        }
    }
}
