using ReactViewResources;
using System;
using System.IO;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {

            private const string JavascriptExtension = ".js";

            private readonly ReactViewRender owner;

            public InternalWebView(ReactViewRender owner, bool preloadBrowser) {
                this.owner = owner;
                IsSecurityDisabled = true; // must be set before InitializeBrowser

                // service worker scripts must be handled in a special way
                RegisterProtocolHandler(Uri.UriSchemeHttps, new ExtendedCefResourceHandlerFactory(this));

                if (preloadBrowser) {
                    InitializeBrowser();
                }
            }

            protected override string GetRequestUrl(string url, ResourceType resourceType) {
                if (resourceType == ResourceType.Script || resourceType == ResourceType.Xhr) {
                    var uri = new UriBuilder(url);
                    if (Path.GetExtension(uri.Path) == "") {
                        // dependency modules fetched by requirejs do not come with the js extension :(
                        uri.Path += JavascriptExtension;
                        return uri.ToString();
                    }
                }

                return base.GetRequestUrl(url, resourceType);
            }

            private class ExtendedCefResourceHandlerFactory : CefResourceHandlerFactory {

                public ExtendedCefResourceHandlerFactory(WebView webview) : base(webview) {
                }

                protected override void HandleRequest(ResourceHandler resourceHandler) {
                    if (resourceHandler.Url.EndsWith("/sw.js")) {
                        var serviceWorkerScript = ResourcesManager.GetResource(typeof(Resources).Assembly, new[] { "sw.js" });
                        resourceHandler.RespondWith(serviceWorkerScript, ".js");
                        return;
                    }
                    base.HandleRequest(resourceHandler);
                }
            }
        }
    }
}
