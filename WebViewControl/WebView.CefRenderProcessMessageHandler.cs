using CefSharp;
using System.Linq;
using System;

namespace WebViewControl {

    partial class WebView {

        private class CefRenderProcessMessageHandler : IRenderProcessMessageHandler {

            private readonly WebView OwnerWebView;

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
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextCreated.Invoke());
                    }
                }
            }

            public void OnContextReleased(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
                if (!IgnoreEvent(frame)) {
                    var javascriptContextReleased = OwnerWebView.JavascriptContextReleased;
                    if (javascriptContextReleased != null) {
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextReleased.Invoke());
                    }
                }
            }

            public void OnFocusedNodeChanged(IWebBrowser browserControl, IBrowser browser, IFrame frame, IDomNode node) { }

            public void OnUncaughtException(IWebBrowser browserControl, IBrowser browser, IFrame frame, CefSharp.JavascriptException exception) {
                if (JavascriptExecutor.IsInternalException(exception.Message)) {
                    // ignore internal exceptions, they will be handled by the EvaluateScript caller
                    return;
                }
                var javascriptException = new JavascriptException(
                    exception.Message, 
                    exception.StackTrace.Select(l => {
                        var location = l.SourceName + ":" + l.LineNumber + ":" + l.ColumnNumber;
                        return JavascriptException.AtSeparator + (string.IsNullOrEmpty(l.FunctionName) ? location : l.FunctionName + " (" + location + ")");
                    }).ToArray());
                OwnerWebView.ForwardUnhandledAsyncException(javascriptException);
            }
        }
    }
}
