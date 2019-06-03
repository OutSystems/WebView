using System.IO;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        internal class CefResourceHandler : CefSharp.ResourceHandler {

            private string RedirectUrl { get; }

            public CefResourceHandler(string redirectUrl) {
                RedirectUrl = redirectUrl;
            }

            public override Stream GetResponse(IResponse response, out long responseLength, out string redirectUrl) {
                responseLength = 0;
                redirectUrl = RedirectUrl;
                return null;
            }
        }

    }
}
