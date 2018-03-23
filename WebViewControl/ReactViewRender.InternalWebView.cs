using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CefSharp;

namespace WebViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {

            private const string JavascriptExtension = ".js";

            private readonly ReactViewRender owner;

            public InternalWebView(ReactViewRender owner) {
                this.owner = owner;
            }

            protected override string GetRequestUrl(IRequest request) {
                if (request.ResourceType == ResourceType.Script) {
                    var url = new UriBuilder(request.Url);
                    if (!url.Path.EndsWith(JavascriptExtension)) {
                        // dependency modules fetched by requirejs do not come with the js extension :(
                        url.Path += JavascriptExtension;
                        return url.ToString();
                    }
                }

                return base.GetRequestUrl(request);
            }

            protected override void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
                if (owner.Mappings != null && owner.Mappings.TryGetValue(url.AbsolutePath, out ResourceUrl mapping)) {
                    url = new Uri(owner.ToFullUrl(mapping.ToString()), UriKind.Absolute);
                }
                base.LoadEmbeddedResource(resourceHandler, url);
            }

            protected override Stream TryGetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
                return base.TryGetResourceWithFullPath(assembly, resourcePath);
            }
        }
    }
}
