using CefSharp;

namespace WebViewControl {

    partial class ReactViewRender {

        private class InternalWebView : WebView {
            protected override string GetRequestUrl(IRequest request) {
                const string JavascriptExtension = ".js";

                if (request.ResourceType == ResourceType.Script && !request.Url.EndsWith(JavascriptExtension)) {
                    // dependency modules fetched by requirejs do not come with the js extension :(
                    return request.Url + JavascriptExtension;
                }

                return base.GetRequestUrl(request);
            }
        }
    }
}
