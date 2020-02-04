using System;
using CefSharp;
using CefSharp.Handler;

namespace WebViewControl {

    partial class WebView {

        private class CefRequestHandler : RequestHandler {

            private WebView OwnerWebView { get; }
            private CefResourceRequestHandler ResourceRequestHandler { get; }

            public CefRequestHandler(WebView webView) {
                OwnerWebView = webView;
                ResourceRequestHandler = new CefResourceRequestHandler(OwnerWebView);
            }

            protected override bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, long newSize, IRequestCallback callback) {
                using (callback) {
                    callback.Continue(true);
                }
                return true;
            }

            protected override bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback) {
                using (callback) {
                    if (OwnerWebView.ProxyAuthentication != null) {
                        callback.Continue(OwnerWebView.ProxyAuthentication.UserName, OwnerWebView.ProxyAuthentication.Password);
                    }
                }
                return true;
            }

            protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect) {
                if (OwnerWebView.FilterUrl(request.Url)) {
                    return false;
                }
                
                if (OwnerWebView.IsHistoryDisabled && (request.TransitionType & TransitionType.ForwardBack) == TransitionType.ForwardBack) {
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

            protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback) {
                using (callback) {
                    if (OwnerWebView.IgnoreCertificateErrors) {
                        callback.Continue(true);
                        return true;
                    }
                }
                return false;
            }

            protected override void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status) {
                OwnerWebView.RenderProcessCrashed?.Invoke();

                const string ExceptionPrefix = "WebView render process ";

                Exception exception;

                switch (status) {
                    case CefTerminationStatus.ProcessCrashed:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "crashed");
                        break;
                    case CefTerminationStatus.ProcessWasKilled:
                        var OnProcessKilledSufix = "The disposing status was: " + OwnerWebView.IsDisposing.ToString();
                        exception = new RenderProcessKilledException(ExceptionPrefix + "was killed. " + OnProcessKilledSufix);
                        break;
                    case CefTerminationStatus.OutOfMemory:
                        exception = new RenderProcessKilledException(ExceptionPrefix + "ran out-of-memory");
                        break;
                    default:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "terminated with an unknown reason");
                        break;
                }

                OwnerWebView.ExecuteWithAsyncErrorHandling(() => throw exception);
            }

            protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling) {
                return ResourceRequestHandler;
            }
        }
    }
}
