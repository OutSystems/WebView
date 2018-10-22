using System;
using CefSharp;

namespace WebViewControl {

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
        }
    }
}
