using System;
using CefSharp;

namespace WebViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {
            protected override string GetRequestUrl(IRequest request) {
                const string JavascriptExtension = ".js";
                
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
