using System.IO;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    internal class AsyncResourceHandler : DefaultResourceHandler {

        private CefCallback responseCallback;

        protected override bool ProcessRequest(CefRequest request, CefCallback callback) {
            if (Response == null && string.IsNullOrEmpty(RedirectUrl)) {
                responseCallback = callback;
            } else {
                callback.Continue();
            }
            return true;
        }

        public void SetResponse(string response, string mimeType = null) {
            Response = GetMemoryStream(response, Encoding.UTF8, false);
            MimeType = mimeType;
        }

        public void SetResponse(Stream response, string mimeType = null) {
            Response = response;
            MimeType = mimeType;
        }

        public void RedirectTo(string targetUrl) {
            RedirectUrl = targetUrl;
        }

        public void Continue() {
            responseCallback?.Continue();
        }

        public static DefaultResourceHandler FromText(string text) {
            var handler = new AsyncResourceHandler();
            handler.SetResponse(text);
            return handler;
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
