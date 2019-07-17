using System.IO;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        internal class CefAsyncResourceHandler : DefaultResourceHandler {

            private CefCallback responseCallback;

            protected override bool ProcessRequest(CefRequest request, CefCallback callback) {
                if (Response == null && string.IsNullOrEmpty(RedirectUrl)) {
                    responseCallback = callback;
                } else {
                    callback.Continue();
                }
                return true;
            }

            public void SetResponse(string response) {
                Response = GetMemoryStream(response, Encoding.UTF8, false);
            }

            public void RedirectTo(string targetUrl) {
                RedirectUrl = targetUrl;
            }

            public void Continue() {
                responseCallback?.Continue();
            }

            private static MemoryStream GetMemoryStream(string text, Encoding encoding, bool includePreamble = true) {
                if (includePreamble) {
                    var preamble = encoding.GetPreamble();
                    var bytes = encoding.GetBytes(text);

                    var memoryStream = new MemoryStream(preamble.Length + bytes.Length);

                    memoryStream.Write(preamble, 0, preamble.Length);
                    memoryStream.Write(bytes, 0, bytes.Length);

                    memoryStream.Position = 0;

                    return memoryStream;
                }

                return new MemoryStream(encoding.GetBytes(text));
            }
        }
    }
}
