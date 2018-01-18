using System;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private class CefRenderProcessMessageHandler : IRenderProcessMessageHandler {

            private readonly WebView OwnerWebView;

            public CefRenderProcessMessageHandler(WebView webView) {
                OwnerWebView = webView;
            }

            public void OnContextCreated(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                if (OwnerWebView.JavascriptContextCreated != null) {
                    OwnerWebView.ExecuteWithAsyncErrorHandling(() => OwnerWebView.JavascriptContextCreated?.Invoke());
                };
            }

            public void OnContextReleased(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                OwnerWebView.JavascriptContextReleased?.Invoke();
            }

            public void OnFocusedNodeChanged(IWebBrowser browserControl, IBrowser browser, IFrame frame, IDomNode node) { }
        }
    }
}
