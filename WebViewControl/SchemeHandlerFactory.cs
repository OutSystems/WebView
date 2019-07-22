using Xilium.CefGlue;

namespace WebViewControl {

    internal class SchemeHandlerFactory : CefSchemeHandlerFactory {

        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request) {
            return null;
        }
    }
}
