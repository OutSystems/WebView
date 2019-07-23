using CefSharp;
using System;

namespace WebViewControl {

    partial class WebView {

        private class CefRenderProcessMessageHandler : IRenderProcessMessageHandler {

            private WebView OwnerWebView { get; }

            public CefRenderProcessMessageHandler(WebView webView) {
                OwnerWebView = webView;
            }

            private static bool IgnoreEvent(IFrame frame) {
                return frame.Url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase);
            }

            public void OnContextCreated(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                if (!IgnoreEvent(frame)) {
                    var javascriptContextCreated = OwnerWebView.JavascriptContextCreated;
                    if (javascriptContextCreated != null) {
                        var frameName = frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextCreated(frameName));
                    }
                }
            }

            public void OnContextReleased(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                if (!IgnoreEvent(frame)) {
                    var javascriptContextReleased = OwnerWebView.JavascriptContextReleased;
                    if (javascriptContextReleased != null) {
                        var frameName = frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextReleased(frameName));
                    }
                }
            }

            public void OnFocusedNodeChanged(IWebBrowser browserControl, IBrowser browser, IFrame frame, IDomNode node) { }

            public void OnUncaughtException(IWebBrowser browserControl, IBrowser browser, IFrame frame, CefSharp.JavascriptException exception) {
                if (JavascriptExecutor.IsInternalException(exception.Message)) {
                    // ignore internal exceptions, they will be handled by the EvaluateScript caller
                    return;
                }
                var javascriptException = new JavascriptException(exception.Message, exception.StackTrace);
                OwnerWebView.ForwardUnhandledAsyncException(javascriptException, frame.Name);
            }
        }
    }
}
