using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private class RenderProcessMessageHandler : IRenderProcessMessageHandler {

            private readonly WebView OwnerWebView;

            public RenderProcessMessageHandler(WebView webView) {
                OwnerWebView = webView;
            }

            public void OnContextCreated(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                if (OwnerWebView.JavascriptContextCreated != null) {
                    OwnerWebView.JavascriptContextCreated();
                }
            }

            public void OnFocusedNodeChanged(IWebBrowser browserControl, IBrowser browser, IFrame frame, IDomNode node) {
                
            }
        }
    }
}
