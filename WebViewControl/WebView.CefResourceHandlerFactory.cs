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

                if (FilterRequest(request)) {
                    return null;
                }

                Uri url;
                var resourceHandler = new ResourceHandler(request);
                if (Uri.TryCreate(resourceHandler.Url, UriKind.Absolute, out url) && url.Scheme == EmbeddedScheme) {
                    OwnerWebView.WithErrorHandling(() => OwnerWebView.LoadEmbeddedResource(resourceHandler, url));
                }
                
                if (OwnerWebView.BeforeResourceLoad != null) {
                    OwnerWebView.WithErrorHandling(() => OwnerWebView.BeforeResourceLoad(resourceHandler));
                }
                
                if (resourceHandler.Handled) {
                    return resourceHandler.Handler;
                } else if (!OwnerWebView.IgnoreMissingResources && url != null && url.Scheme == EmbeddedScheme) {
                    OwnerWebView.WithErrorHandling(() => { throw new InvalidOperationException("Resource not found: '" + request.Url + "'"); });
                }

                return null;
            }
        }

        protected virtual void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
            var resourceAssembly = ResolveResourceAssembly(url);
            var resourcePath = ResolveResourcePath(url, resourceAssembly.GetName().Name);

            var extension = Path.GetExtension(resourcePath.Last()).ToLower();

            var resourceStream = ResourcesManager.TryGetResourceWithFullPath(resourceAssembly, resourcePath);
            if (resourceStream != null) {
                resourceHandler.RespondWith(resourceStream, extension);
            }
        }

        protected virtual Assembly ResolveResourceAssembly(Uri resourceUrl) {
            if (!resourceUrl.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                //if (resourcesSource != null) {
                //    return resourcesSource;
                //}
            }

            if (assemblies == null) {
                assemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(a => a.GetName().Name, a => a);
            }
            Assembly assembly;
            var assemblyName = GetEmbeddedResourceAssemblyName(resourceUrl);
            if (assemblies.TryGetValue(assemblyName, out assembly)) {
                return assembly;
            }
            
            throw new InvalidOperationException("Could not find assembly for: " + resourceUrl);
        }

        /// <summary>
        /// Supported sintax:
        /// embedded://webview/assembly:AssemblyName;Path/To/Resource
        /// embedded://webview/AssemblyName/Path/To/Resource (AssemblyName is also assumed as default namespace)
        /// </summary>
        protected virtual string[] ResolveResourcePath(Uri resourceUrl, string assemblyName) {
            if (resourceUrl.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                return GetEmbeddedResourcePath(resourceUrl).Split('/');
            }
            var uriParts = resourceUrl.Segments;
            return uriParts.Skip(1).Select(p => p.Replace("/", "")).ToArray();
        }
    }
}
