using System;
using CefSharp;

namespace WebViewControl {

    partial class WebView {
        private class CefSchemeHandlerFactory : ISchemeHandlerFactory {

            private readonly Action<ResourceHandler> requestHandler;

            public CefSchemeHandlerFactory(Action<ResourceHandler> requestHandler = null) {
                this.requestHandler = requestHandler;
            }

            public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request) {
                if (requestHandler != null) {
                    var resourceHandler = new ResourceHandler(request, null);
                    requestHandler.Invoke(resourceHandler);
                    if (resourceHandler.Handled) {
                        return resourceHandler.Handler;
                    }
                }
                return null;
            }
        }
    }
}
