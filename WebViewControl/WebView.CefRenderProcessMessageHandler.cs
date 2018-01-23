using CefSharp;
using System.Linq;

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
