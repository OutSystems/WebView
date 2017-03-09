using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private class CefResourceHandlerFactory : IResourceHandlerFactory {

            private readonly WebView OwnerWebView;

            public CefResourceHandlerFactory(WebView webView) {
                OwnerWebView = webView;
            }

            public bool HasHandlers {
                get { return true; }
            }

            IResourceHandler IResourceHandlerFactory.GetResourceHandler(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request) {
                if (request.Url == DefaultLocalUrl) {
                    return OwnerWebView.htmlToLoad != null ? CefSharp.ResourceHandler.FromString(OwnerWebView.htmlToLoad, "html") : null;
                }

                if (request.Url != DefaultEmbeddedUrl && FilterRequest(request)) {
                    return null;
                }
                
                var resourceHandler = new ResourceHandler(request);
                if (OwnerWebView.BeforeResourceLoad != null) {
                    OwnerWebView.BeforeResourceLoad(resourceHandler);
                }

                if (!resourceHandler.Handled && OwnerWebView.resourcesSource != null) {
                    OwnerWebView.LoadEmbeddedResource(resourceHandler);
                }
                return resourceHandler.Handler;
            }
        }

        private void LoadEmbeddedResource(ResourceHandler resourceHandler) {
            if (!Uri.IsWellFormedUriString(resourceHandler.Url, UriKind.Absolute)) {
                return;
            }

            var uri = new Uri(resourceHandler.Url);
            if (uri.Scheme != EmbeddedScheme) {
                return;
            }

            var resourceAssembly = ResolveResourceAssembly(uri);
            var resourcePath = ResolveResourcePath(uri);

            var extension = Path.GetExtension(resourcePath.Last()).ToLower();

            resourceHandler.RespondWith(ResourcesManager.GetResource(resourceAssembly, resourcePath), extension);
        }

        protected virtual Assembly ResolveResourceAssembly(Uri resourceUrl) {
            return resourcesSource;
        }

        protected virtual string[] ResolveResourcePath(Uri resourceUrl) {
            var uriParts = resourceUrl.Segments;
            return (new[] { resourcesNamespace }.Concat(uriParts.Skip(1).Select(p => p.Replace("/", "")))).ToArray();
        }
    }
}
