using System;
using Xilium.CefGlue;

namespace WebViewControl {

    partial class WebView {

        private class CefRenderProcessMessageHandler : Xilium.CefGlue.CefRenderProcessHandler {

            private WebView OwnerWebView { get; }

            public CefRenderProcessMessageHandler(WebView webView) {
                OwnerWebView = webView;
            }

            private static bool IgnoreEvent(CefFrame frame) {
                return frame.Url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase);
            }

            protected override void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context) {
                if (!IgnoreEvent(frame)) {
                    var javascriptContextCreated = OwnerWebView.JavascriptContextCreated;
                    if (javascriptContextCreated != null) {
                        var frameName = frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextCreated(frameName));
                    }
                }
            }

            protected override void OnContextReleased(CefBrowser browser, CefFrame frame, CefV8Context context) {
                if (!IgnoreEvent(frame)) {
                    var javascriptContextReleased = OwnerWebView.JavascriptContextReleased;
                    if (javascriptContextReleased != null) {
                        var frameName = frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => javascriptContextReleased(frameName));
                    }
                }
            }

            protected override void OnUncaughtException(CefBrowser browser, CefFrame frame, CefV8Context context, CefV8Exception exception, CefV8StackTrace stackTrace) {
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
