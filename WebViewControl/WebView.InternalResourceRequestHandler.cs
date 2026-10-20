using System;
using Xilium.CefGlue;

namespace WebViewControl {

    partial class WebView {

        private class InternalResourceRequestHandler : CefResourceRequestHandler {

            public InternalResourceRequestHandler(WebView ownerWebView) {
                OwnerWebView = ownerWebView;
            }

            private WebView OwnerWebView { get; }

            protected override CefCookieAccessFilter GetCookieAccessFilter(CefBrowser browser, CefFrame frame, CefRequest request) {
                return null;
            }

            protected override CefResourceHandler GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request) {
                if (OwnerWebView.TryGetHtmlToLoad(request.Url, out var html)) {
                    return AsyncResourceHandler.FromText(html);
                }

                if (UrlHelper.IsChromeInternalUrl(request.Url)) {
                    return null;
                }

                var resourceHandler = OwnerWebView.HandleResourceRequest(new CefRequestDetails(request), CreateResponse);

                return (AsyncResourceHandler) resourceHandler.Handler;
            }

            private static IResourceResponse CreateResponse() {
                var handler = new AsyncResourceHandler();
                handler.Headers.Add("cache-control", "public, max-age=315360000");
                return handler;
            }
        }
    }
}
