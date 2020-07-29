using System;
using System.IO;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    internal class AsyncResourceHandler : DefaultResourceHandler {

        private CefCallback responseCallback;
        private bool autoDisposeStream;

        protected override RequestHandlingFashion ProcessRequestAsync(CefRequest request, CefCallback callback) {
            lock (SyncRoot) {
                if (Response == null && string.IsNullOrEmpty(RedirectUrl)) {
                    responseCallback = callback;
                    return RequestHandlingFashion.ContinueAsync;
                }
                return RequestHandlingFashion.Continue;
            }
        }

        public void SetResponse(string response, string mimeType = null) {
            var responseStream = GetMemoryStream(response, Encoding.UTF8, includePreamble: true);
            SetResponse(responseStream, mimeType, autoDisposeStream);
        }

        public void SetResponse(Stream response, string mimeType = null, bool autoDisposeStream = false) {
            lock (SyncRoot) {
                Response = response;
                MimeType = mimeType;
                this.autoDisposeStream = autoDisposeStream;
            }
        }

        public void RedirectTo(string targetUrl) {
            lock (SyncRoot) {
                RedirectUrl = targetUrl;
            }
        }

        public void Continue() {
            lock (SyncRoot) {
                if (responseCallback != null) {
                    using (responseCallback) {
                        responseCallback.Continue();
                    }
                }
            }
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

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (autoDisposeStream) {
                var response = Response;
                if (response != null) {
                    lock (SyncRoot) {
                        response.Dispose();
                    }
                }
            }
        }

        #region Legacy NetworkService

        [Obsolete]
        protected override bool ProcessRequest(CefRequest request, CefCallback callback) {
            lock (SyncRoot) {
                if (Response == null && string.IsNullOrEmpty(RedirectUrl)) {
                    responseCallback = callback;
                } else {
                    callback.Continue();
                }
                return true;
            }
        }

        [Obsolete]
        protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback) {
            callback.Dispose();

            if (Response == null) {
                bytesRead = 0;
                return false;
            }

            var buffer = new byte[response.Length];
            bytesRead = Response.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0) {
                return false;
            }

            response.Write(buffer, 0, buffer.Length);

            return bytesRead > 0;
        }

        #endregion
    }
}
