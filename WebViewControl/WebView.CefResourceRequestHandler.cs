using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CefSharp;
using CefSharp.Handler;

namespace WebViewControl {

    partial class WebView {

        protected class CefResourceRequestHandler : ResourceRequestHandler, IDisposable {

            private WebView OwnerWebView { get; }

            public CefResourceRequestHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override IResourceHandler GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request) {
                if (request.Url == OwnerWebView.DefaultLocalUrl) {
                    return CefSharp.ResourceHandler.FromString(OwnerWebView.htmlToLoad ?? "");
                }

                if (OwnerWebView.FilterUrl(request.Url)) {
                    return null;
                }

                var resourceHandler = new ResourceHandler(request, OwnerWebView.GetRequestUrl(request.Url, (ResourceType)request.ResourceType));
                HandleRequest(resourceHandler);
                return resourceHandler.Handler;
            }

            protected void HandleRequest(ResourceHandler resourceHandler) {
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

                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => LoadEmbeddedResource(resourceHandler, urlWithoutQuery.Uri));

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

                    return;
                }

                TriggerBeforeResourceLoadEvent();
            }

            protected void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
                var resource = ResourcesManager.LoadEmbeddedResource(url);

                if (resource.ResourceStream != null) {
                    resourceHandler.RespondWith(resource.ResourceStream, resource.Extension);
                }
            }

            protected Stream TryGetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
                return ResourcesManager.TryGetResourceWithFullPath(assembly, resourcePath);
            }

            public void Dispose() { }
        }
    }
}
