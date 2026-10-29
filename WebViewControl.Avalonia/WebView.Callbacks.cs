using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebViewControl.Native;

namespace WebViewControl {

    partial class WebView {

        // ---- resources -----------------------------------------------------------------------------------------------

        void INativeBrowserOwner.OnResourceRequested(ResourceRequest request) {
            if (isDisposing) {
                request.Complete(null);
                return;
            }

            if (request.Url == DefaultLocalUrl) {
                // the html set with LoadHtml
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(htmlToLoad ?? string.Empty);
                request.Complete(new ResourceResponse { MimeType = "text/html", Content = new MemoryStream(bytes) });
                return;
            }

            // the handlers can be slow (and can block waiting for the UI thread), do not run them in the UI thread
            Task.Run(async () => {
                ResourceResponse response = null;
                try {
                    var resourceHandler = new ResourceHandler(request.Method, request.Url, GetRequestUrl(request.Url, ResourceType.Unknown), request.Headers, e => ForwardUnhandledAsyncException(e));
                    await HandleResourceRequest(resourceHandler).ConfigureAwait(false);
                    if (resourceHandler.Handled) {
                        response = await CreateResponseAsync(resourceHandler, redirectsFollowed: 0).ConfigureAwait(false);
                    }
                } catch (Exception e) {
                    ForwardUnhandledAsyncException(e);
                }
                request.Complete(response);
            });
        }

        private async Task HandleResourceRequest(ResourceHandler resourceHandler) {
            void TriggerBeforeResourceLoadEvent() {
                var beforeResourceLoad = BeforeResourceLoad;
                if (beforeResourceLoad != null) {
                    ExecuteWithAsyncErrorHandling(() => beforeResourceLoad(resourceHandler));
                }
            }

            if (Uri.TryCreate(resourceHandler.Url, UriKind.Absolute, out var url) && url.Scheme == ResourceUrl.EmbeddedScheme) {
                var urlWithoutQuery = new UriBuilder(url);
                if (!string.IsNullOrEmpty(url.Query)) {
                    urlWithoutQuery.Query = string.Empty;
                }

                ExecuteWithAsyncErrorHandling(() => resourceHandler.LoadEmbeddedResource(urlWithoutQuery.Uri));

                TriggerBeforeResourceLoadEvent();

                await resourceHandler.WaitForAsyncResponsesAsync().ConfigureAwait(false);

                if (resourceHandler.Handled || IgnoreMissingResources) {
                    return;
                }

                var resourceLoadFailed = ResourceLoadFailed;
                if (resourceLoadFailed != null) {
                    resourceLoadFailed(url.ToString());
                } else {
                    ForwardUnhandledAsyncException(new InvalidOperationException("Resource not found: " + url));
                }
            } else {
                TriggerBeforeResourceLoadEvent();
                await resourceHandler.WaitForAsyncResponsesAsync().ConfigureAwait(false);
            }
        }

        private const int MaxRedirects = 10;

        private static readonly Lazy<HttpClient> RedirectsHttpClient = new Lazy<HttpClient>(() => new HttpClient());

        private async Task<ResourceResponse> CreateResponseAsync(ResourceHandler resourceHandler, int redirectsFollowed) {
            if (string.IsNullOrEmpty(resourceHandler.RedirectUrl)) {
                return new ResourceResponse {
                    MimeType = resourceHandler.MimeType,
                    Headers = resourceHandler.ResponseHeaders,
                    Content = resourceHandler.Response,
                    AutoDisposeContent = resourceHandler.AutoDisposeStream
                };
            }

            // The native web views do not follow redirects of the custom schemes, so the redirect is resolved here and its content
            // is served in place of the original url. As with the cef based web view, a redirect wins over a response that was also set.
            if (resourceHandler.AutoDisposeStream) {
                resourceHandler.Response?.Dispose();
            }

            if (redirectsFollowed >= MaxRedirects) {
                throw new InvalidOperationException($"Too many redirects loading {resourceHandler.Url}");
            }

            var target = resourceHandler.RedirectUrl;
            if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri)) {
                throw new InvalidOperationException($"Invalid redirect url '{target}' loading {resourceHandler.Url}");
            }

            if (targetUri.Scheme == Uri.UriSchemeHttp || targetUri.Scheme == Uri.UriSchemeHttps) {
                using (var httpResponse = await RedirectsHttpClient.Value.GetAsync(targetUri, AsyncCancellationTokenSource.Token).ConfigureAwait(false)) {
                    var content = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return new ResourceResponse {
                        StatusCode = (int)httpResponse.StatusCode,
                        MimeType = httpResponse.Content.Headers.ContentType?.ToString(),
                        Content = new MemoryStream(content),
                        AutoDisposeContent = true
                    };
                }
            }

            // embedded, custom, ... urls go through the resources pipeline again
            var redirectHandler = new ResourceHandler(resourceHandler.Method, target, GetRequestUrl(target, ResourceType.Unknown), resourceHandler.GetHeaderMap(), e => ForwardUnhandledAsyncException(e));
            await HandleResourceRequest(redirectHandler).ConfigureAwait(false);
            return redirectHandler.Handled ? await CreateResponseAsync(redirectHandler, redirectsFollowed + 1).ConfigureAwait(false) : null;
        }

        // ---- navigation ----------------------------------------------------------------------------------------------

        bool INativeBrowserOwner.OnNavigating(string url, string method, bool isMainFrame, bool isBackForward) {
            if (isDisposing || url == null || UrlHelper.IsInternalUrl(url)) {
                return false;
            }

            if (IsHistoryDisabled && isBackForward) {
                return true;
            }

            var beforeNavigate = BeforeNavigate;
            if (beforeNavigate != null) {
                var request = new Request(method, url, GetRequestUrl(url, ResourceType.Unknown), null, isMainFrame);
                ExecuteWithAsyncErrorHandling(() => beforeNavigate(request));
                return request.Canceled;
            }

            return false;
        }

        void INativeBrowserOwner.OnNavigationCommitted(string url) {
            // the previous document is gone
            HandleContextReleased();
        }

        void INativeBrowserOwner.OnNavigationFinished(string url) {
            if (url == null || UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (url == DefaultLocalUrl) {
                // finished loading local url, discard html
                htmlToLoad = null;
            }

            if (Navigated != null) {
                AsyncExecuteInUI(() => Navigated?.Invoke(url, string.Empty));
            }
        }

        void INativeBrowserOwner.OnNavigationFailed(string url, int errorCode, string description) {
            if (url != null && UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (url == DefaultLocalUrl) {
                // failed loading default local url, discard html
                htmlToLoad = null;
            }

            if (LoadFailed != null) {
                AsyncExecuteInUI(() => LoadFailed?.Invoke(url, errorCode, string.Empty));
            }
        }

        void INativeBrowserOwner.OnTitleChanged(string title) {
            TitleChanged?.Invoke();
        }

        void INativeBrowserOwner.OnAddressChanged(string url) {
            AsyncExecuteInUI(() => Address = url);
        }

        void INativeBrowserOwner.OnProcessTerminated() {
            // scripts that were running will never complete
            HandleContextReleased();
            ForwardUnhandledAsyncException(new RenderProcessCrashedException("WebView render process terminated"));
        }

        bool INativeBrowserOwner.OnPopupRequested(string url) {
            if (url == null || !Uri.IsWellFormedUriString(url, UriKind.Absolute)) {
                return false;
            }

            try {
                var popupOpening = PopupOpening;
                if (popupOpening != null) {
                    popupOpening(url);
                } else {
                    UrlHelper.OpenInExternalBrowser(url);
                }
            } catch {
                // do not blow in the users face
                return false;
            }
            return true;
        }

        bool INativeBrowserOwner.OnJavascriptDialog(JavascriptDialogKind kind, string message, string defaultValue, Action<bool, string> complete) {
            var javascriptDialogShown = JavacriptDialogShown;
            if (javascriptDialogShown == null) {
                return false;
            }

            javascriptDialogShown(message, () => complete(true, string.Empty));
            return true;
        }

        void INativeBrowserOwner.OnError(Exception exception) {
            ForwardUnhandledAsyncException(exception);
        }

        bool INativeBrowserOwner.OnAuthenticationRequested(bool isProxy, string host, int port, string realm, out string userName, out string password) {
            // same as the cef based web view: the proxy authentication is provided to every authentication request
            var credentials = ProxyAuthentication;
            userName = credentials?.UserName;
            password = credentials?.Password;
            return credentials != null;
        }

        bool INativeBrowserOwner.OnCertificateError(string host) => IgnoreCertificateErrors;
    }

    internal static class ResourceHandlerExtensions {

        public static void LoadEmbeddedResource(this ResourceHandler resourceHandler, Uri url) {
            var stream = ResourcesManager.TryGetResource(url, true, out string extension);

            if (stream != null) {
                resourceHandler.RespondWith(stream, extension);
            }
        }
    }
}
