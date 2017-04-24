using System.IO;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        internal class CefResourceHandler : CefSharp.ResourceHandler {

            private readonly string redirectUrl;

            public CefResourceHandler(string redirectUrl) {
                this.redirectUrl = redirectUrl;
            }

            public override Stream GetResponse(IResponse response, out long responseLength, out string redirectUrl) {
                responseLength = 0;
                redirectUrl = this.redirectUrl;
                return null;
            }
        }

    }
}
