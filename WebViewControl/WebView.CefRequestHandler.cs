using System;
using Xilium.CefGlue;

namespace WebViewControl {

    partial class WebView {

        private class CefRequestHandler : Xilium.CefGlue.Common.Handlers.RequestHandler {

            private WebView OwnerWebView { get; }

            public CefRequestHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnQuotaRequest(CefBrowser browser, string originUrl, long newSize, CefRequestCallback callback) {
                callback.Continue(true);
                return true;
            }

            protected override bool GetAuthCredentials(CefBrowser browser, CefFrame frame, bool isProxy, string host, int port, string realm, string scheme, CefAuthCallback callback) {
                if (OwnerWebView.ProxyAuthentication != null) {
                    callback.Continue(OwnerWebView.ProxyAuthentication.UserName, OwnerWebView.ProxyAuthentication.Password);
                }
                return true;
            }

            protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect) {
                if (OwnerWebView.FilterUrl(request.Url)) {
                    return false;
                }
                
                if (OwnerWebView.IsHistoryDisabled && request.TransitionType.HasFlag(CefTransitionType.ForwardBackFlag)) {
                    return true;
                }
               
                bool cancel = false;
                if (OwnerWebView.BeforeNavigate != null) {
                    var wrappedRequest = new Request(request, OwnerWebView.GetRequestUrl(request.Url, (ResourceType) request.ResourceType));
                    OwnerWebView.ExecuteWithAsyncErrorHandling(() => OwnerWebView.BeforeNavigate(wrappedRequest));
                    cancel = wrappedRequest.Canceled;
                }

                return cancel;
            }

            protected override bool OnCertificateError(CefBrowser browser, CefErrorCode certError, string requestUrl, CefSslInfo sslInfo, CefRequestCallback callback) {
                if (OwnerWebView.IgnoreCertificateErrors) {
                    callback.Continue(true);
                    return true;
                }
                return false;
            }

            protected override void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status) {
                OwnerWebView.RenderProcessCrashed?.Invoke();

                const string ExceptionPrefix = "WebView render process ";

                Exception exception;

                switch (status) {
                    case CefTerminationStatus.ProcessCrashed:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "crashed");
                        break;
                    case CefTerminationStatus.WasKilled:
                        exception = new RenderProcessKilledException(ExceptionPrefix + "was killed");
                        break;
                    case CefTerminationStatus.OutOfMemory:
                        exception = new RenderProcessKilledException(ExceptionPrefix + "ran out of memory");
                        break;
                    default:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "terminated with an unknown reason");
                        break;
                }

                OwnerWebView.ExecuteWithAsyncErrorHandling(() => throw exception);
            }
        }
    }
}
