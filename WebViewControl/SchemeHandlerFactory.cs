using System.Linq;
using Xilium.CefGlue;

namespace WebViewControl {

    internal class SchemeHandlerFactory : CefSchemeHandlerFactory {

        private WebView OwnerWebView { get; }

        public SchemeHandlerFactory(WebView ownerWebView) {
            OwnerWebView = ownerWebView;
        }

        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request) {
            if (OwnerWebView.IsSecurityDisabled && HttpsResourceHandler.AcceptedResources.Contains(request.ResourceType)) {
                return new HttpsResourceHandler();
            }
            return null;
        }
    }
}
