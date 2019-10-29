using System.IO;
using System.Text;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        internal class CefAsyncResourceHandler : CefSharp.ResourceHandler, IResourceHandler {

            private ICallback responseCallback;
            private string redirectUrl;

            public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback) {
                if (Stream == null && string.IsNullOrEmpty(redirectUrl)) {
                    responseCallback = callback;
                    return CefReturnValue.ContinueAsync;
                }
                return CefReturnValue.Continue;
            }

            public void SetResponse(Stream response, string mimeType = CefSharp.ResourceHandler.DefaultMimeType, bool autoDisposeStream = false) {
                if (response?.CanSeek == true) {
                    // move stream to the beginning
                    response.Position = 0;
                }
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
                if (responseCallback != null) {
                    using (responseCallback) {
                        responseCallback.Continue();
                    }
                }
            }

            void IResourceHandler.GetResponseHeaders(IResponse response, out long responseLength, out string redirectUrl) {
                // copied from cefsharp implementation, because there's no overridable method
                redirectUrl = this.redirectUrl;
                responseLength = -1;

                response.MimeType = MimeType;
                response.StatusCode = StatusCode;
                response.StatusText = StatusText;
                response.Headers = Headers;

                if (!string.IsNullOrEmpty(Charset)) {
                    response.Charset = Charset;
                }

                if (ResponseLength.HasValue) {
                    responseLength = ResponseLength.Value;
                } else if (Stream != null && Stream.CanSeek) {
                    //If no ResponseLength provided then attempt to infer the length
                    responseLength = Stream.Length;
                }
            }
        }
    }
}
