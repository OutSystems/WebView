using System;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {

            private const string JavascriptExtension = ".js";

            private readonly ReactViewRender owner;

            public InternalWebView(ReactViewRender owner, bool preloadBrowser) {
                this.owner = owner;
                IsSecurityDisabled = true; // must be set before InitializeBrowser
                if (preloadBrowser) {
                    InitializeBrowser();
                }
            }

            protected override string GetRequestUrl(string url, ResourceType resourceType) {
                if (resourceType == ResourceType.Script) {
                    var uri = new UriBuilder(url);
                    if (!uri.Path.EndsWith(JavascriptExtension)) {
                        // dependency modules fetched by requirejs do not come with the js extension :(
                        uri.Path += JavascriptExtension;
                        return uri.ToString();
                    }
                }

                return base.GetRequestUrl(url, resourceType);
            }
        }
    }
}
