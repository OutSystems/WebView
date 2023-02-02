using Xilium.CefGlue;

namespace WebViewControl {

    internal class HttpResourceRequestHandler : CefResourceRequestHandler {

        protected override CefCookieAccessFilter GetCookieAccessFilter(CefBrowser browser, CefFrame frame, CefRequest request) {
            return null;
        }

        protected override CefResourceHandler GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request) {
            return new HttpResourceHandler();
        }
    }
}
