using System.IO;
using System.Text;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        internal class CefAsyncResourceHandler : CefSharp.ResourceHandler {

            private ICallback responseCallback;
            private string redirectUrl;

            public override bool ProcessRequestAsync(IRequest request, ICallback callback) {
                if (Stream == null && string.IsNullOrEmpty(redirectUrl)) {
                    responseCallback = callback;
                } else {
                    callback.Continue();
                }
                return true;
            }

            public void SetResponse(Stream response, string mimeType = CefSharp.ResourceHandler.DefaultMimeType, bool autoDisposeStream = false) {
                Stream = response;
                MimeType = mimeType;
                AutoDisposeStream = autoDisposeStream;
            }

            public void SetResponse(string response) {
                SetResponse(CefSharp.ResourceHandler.GetMemoryStream(response, Encoding.UTF8, false), autoDisposeStream: true);
            }

            public void RedirectTo(string targetUrl) {
                redirectUrl = targetUrl;
            }

            public void Continue() {
                responseCallback?.Continue();
            }

            public override Stream GetResponse(IResponse response, out long responseLength, out string redirectUrl) {
                if (!string.IsNullOrEmpty(this.redirectUrl)) {
                    responseLength = 0;
                    redirectUrl = this.redirectUrl;
                    return null;
                }
                return base.GetResponse(response, out responseLength, out redirectUrl);
            }
        }
    }
}
