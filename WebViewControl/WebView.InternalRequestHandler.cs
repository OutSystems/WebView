using System;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalRequestHandler : RequestHandler, IDisposable {

            private WebView OwnerWebView { get; }
            private ResourcesProvider ResourcesProvider { get; } = new ResourcesProvider();

            public InternalRequestHandler(WebView webView) {
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
                if (FilterUrl(request.Url)) {
                    return false;
                }
                
                if (OwnerWebView.IsHistoryDisabled && request.TransitionType.HasFlag(CefTransitionType.ForwardBackFlag)) {
                    return true;
                }
               
                var cancel = false;
                var beforeNavigate = OwnerWebView.BeforeNavigate;
                if (beforeNavigate != null) {
                    var wrappedRequest = new Request(request, OwnerWebView.GetRequestUrl(request.Url, (ResourceType) request.ResourceType));
                    OwnerWebView.ExecuteWithAsyncErrorHandling(() => beforeNavigate(wrappedRequest));
                    cancel = wrappedRequest.Canceled;
                }

                return cancel;
            }

            protected override CefResourceHandler GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request) {
                if (request.Url == OwnerWebView.DefaultLocalUrl) {
                    return OwnerWebView.htmlToLoad != null ? AsyncResourceHandler.FromText(OwnerWebView.htmlToLoad) : null;
                }

                if (FilterUrl(request.Url)) {
                    return null;
                }

                var resourceHandler = new ResourceHandler(request, OwnerWebView.GetRequestUrl(request.Url, (ResourceType)request.ResourceType));

                void TriggerBeforeResourceLoadEvent() {
                    var beforeResourceLoad = OwnerWebView.BeforeResourceLoad;
                    if (beforeResourceLoad != null) {
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => beforeResourceLoad(resourceHandler));
                    }
                }

                if (Uri.TryCreate(resourceHandler.Url, UriKind.Absolute, out var url) && url.Scheme == ResourceUrl.EmbeddedScheme) {
                    resourceHandler.BeginAsyncResponse(() => {
                        var urlWithoutQuery = new UriBuilder(url);
                        if (!string.IsNullOrEmpty(url.Query)) {
                            urlWithoutQuery.Query = string.Empty;
                        }

                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => ResourcesProvider.LoadEmbeddedResource(resourceHandler, urlWithoutQuery.Uri));

                        TriggerBeforeResourceLoadEvent();

                        if (resourceHandler.Handled || OwnerWebView.IgnoreMissingResources) {
                            return;
                        }

                        var resourceLoadFailed = OwnerWebView.ResourceLoadFailed;
                        if (resourceLoadFailed != null) {
                            resourceLoadFailed(url.ToString());
                        } else {
                            OwnerWebView.ExecuteWithAsyncErrorHandling(() => throw new InvalidOperationException("Resource not found: " + url));
                        }
                    });
                } else {
                    TriggerBeforeResourceLoadEvent();
                }
                
                return resourceHandler.Handler;
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

            public void Dispose() {
                ResourcesProvider.Dispose();
            }

            private bool FilterUrl(string url) {
                return UrlHelper.IsChromeInternalUrl(url) || url.Equals(OwnerWebView.DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}
