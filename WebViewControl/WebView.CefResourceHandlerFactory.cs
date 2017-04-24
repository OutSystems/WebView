using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private Dictionary<string, Assembly> assemblies;

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
                OwnerWebView.LoadEmbeddedResource(resourceHandler);

                if (OwnerWebView.BeforeResourceLoad != null) {
                    OwnerWebView.BeforeResourceLoad(resourceHandler);
                }

                if (resourceHandler.Handled) {
                    return resourceHandler.Handler;
                }

                return null;
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
            var resourcePath = ResolveResourcePath(uri, resourceAssembly.GetName().Name);

            var extension = Path.GetExtension(resourcePath.Last()).ToLower();

            Stream resourceStream;
            if (IgnoreNonExistingEmbeddedResources) {
                resourceStream = ResourcesManager.TryGetResourceWithFullPath(resourceAssembly, resourcePath);
                if (resourceStream == null) {
                    return;
                }
            } else {
                resourceStream = ResourcesManager.GetResourceWithFullPath(resourceAssembly, resourcePath);
            }

            resourceHandler.RespondWith(resourceStream, extension);
        }

        protected virtual Assembly ResolveResourceAssembly(Uri resourceUrl) {
            if (resourceUrl.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                if (assemblies == null) {
                    assemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(a => a.GetName().Name, a => a);
                }
                Assembly assembly;
                var assemblyName = GetEmbeddedResourceAssemblyName(resourceUrl);
                if (assemblies.TryGetValue(assemblyName, out assembly)) {
                    return assembly;
                }
            }
            if (resourcesSource != null) {
                return resourcesSource;
            }
            throw new InvalidOperationException("Could not find assembly for: " + resourceUrl);
        }

        protected virtual string[] ResolveResourcePath(Uri resourceUrl, string assemblyName) {
            if (resourceUrl.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                return GetEmbeddedResourcePath(resourceUrl).Split('/');
            }
            var uriParts = resourceUrl.Segments;
            return (new[] { assemblyName, resourcesNamespace }.Concat(uriParts.Skip(1).Select(p => p.Replace("/", "")))).ToArray();
        }
    }
}
