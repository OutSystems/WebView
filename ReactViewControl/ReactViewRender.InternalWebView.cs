using ReactViewResources;
using System;
using System.IO;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {

            private const string JavascriptExtension = ".js";

            private ReactViewRender Owner { get; }

            public InternalWebView(ReactViewRender owner, bool preloadBrowser) {
                Owner = owner;
                IsSecurityDisabled = true; // must be set before InitializeBrowser

                if (preloadBrowser) {
                    InitializeBrowser();
                }
            }

            protected override string GetRequestUrl(string url, ResourceType resourceType) {
                if (resourceType == ResourceType.Script || resourceType == ResourceType.Xhr) {
                    var uri = new UriBuilder(url);
                    if (Path.GetExtension(uri.Path) != ".js") {
                        // dependency modules fetched by requirejs do not come with the js extension :(
                        uri.Path += JavascriptExtension;
                        return uri.ToString();
                    }
                }

                return base.GetRequestUrl(url, resourceType);
            }

            protected override bool UseSharedDomain => true; // must be true for the local storage to be shared
        }
    }
}
