using System;
using CefSharp;

namespace WebViewControl {

    partial class WebView {
        private class CefSchemeHandlerFactory : ISchemeHandlerFactory {

            public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request) {
                return null;
            }
        }
    }
}
