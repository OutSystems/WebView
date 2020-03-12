using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalJsDialogHandler : JSDialogHandler {

            private WebView OwnerWebView { get; }

            public InternalJsDialogHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnJSDialog(CefBrowser browser, string originUrl, CefJSDialogType dialogType, string message_text, string default_prompt_text, CefJSDialogCallback callback, out bool suppress_message) {
                suppress_message = false;

                var javacriptDialogShown = OwnerWebView.JavacriptDialogShown;
                if (javacriptDialogShown == null) {
                    return false;
                }

                void Close() {
                    callback.Continue(true, "");
                    callback.Dispose();
                }

                javacriptDialogShown.Invoke(message_text, Close);
                return true;
            }

            protected override bool OnBeforeUnloadDialog(CefBrowser browser, string messageText, bool isReload, CefJSDialogCallback callback) {
                return false; // use default
            }

            protected override void OnResetDialogState(CefBrowser browser) { }

            protected override void OnDialogClosed(CefBrowser browser) { }
        }
    }
}
