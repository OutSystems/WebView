using System.Collections.Generic;
using CefSharp;

namespace WebViewControl {

    partial class WebView {
        private class CefDialogHandler : IDialogHandler {

            private readonly WebView OwnerWebView;

            public CefDialogHandler(WebView webView) {
                OwnerWebView = webView;
            }

            bool IDialogHandler.OnFileDialog(IWebBrowser browserControl, IBrowser browser, CefFileDialogMode mode, CefFileDialogFlags flags, string title, string defaultFilePath, List<string> acceptFilters, int selectedAcceptFilter, IFileDialogCallback callback) {
                if (OwnerWebView.DisableFileDialogs) {
                    return true;
                }
                return false;
            }
        }
    }
}
