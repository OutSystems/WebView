using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WebViewControl {

    public sealed class ResourceHandler : Request {

        private readonly object syncRoot = new object();
        private readonly List<Task> asyncResponses = new List<Task>();
        private readonly Action<Exception> asyncErrorHandler;

        internal ResourceHandler(string method, string url, string urlOverride, NameValueCollection requestHeaders, Action<Exception> asyncErrorHandler)
            : base(method, url, urlOverride, requestHeaders, isMainFrame: false) {
            this.asyncErrorHandler = asyncErrorHandler;
            ResponseHeaders.Add("cache-control", "public, max-age=315360000");
        }

        internal NameValueCollection ResponseHeaders { get; } = new NameValueCollection();

        internal string MimeType { get; private set; }

        internal string RedirectUrl { get; private set; }

        internal bool AutoDisposeStream { get; private set; }

        public Stream Response { get; private set; }

        public bool Handled => Response != null || !string.IsNullOrEmpty(RedirectUrl);

        public void BeginAsyncResponse(Action handleResponse) {
            var task = Task.Run(() => {
                try {
                    handleResponse();
                } catch (Exception e) {
                    // an exception here would leave the request pending forever, report it and let the request complete
                    asyncErrorHandler?.Invoke(e);
                }
            });
            lock (syncRoot) {
                asyncResponses.Add(task);
            }
        }

        /// <summary>
        /// Waits until all the async responses started with <see cref="BeginAsyncResponse"/> (including the ones started by other async responses) are completed.
        /// </summary>
        internal async Task WaitForAsyncResponsesAsync() {
            while (true) {
                Task[] pending;
                lock (syncRoot) {
                    pending = asyncResponses.ToArray();
                    asyncResponses.Clear();
                }
                if (pending.Length == 0) {
                    return;
                }
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
        }

        public void RespondWith(string filename) {
            SetResponse(File.OpenRead(filename), ResourcesManager.GetMimeType(filename), autoDisposeStream: true);
        }

        public void RespondWithText(string text) {
            var encoding = Encoding.UTF8;
            var preamble = encoding.GetPreamble();
            var bytes = encoding.GetBytes(text ?? string.Empty);
            var stream = new MemoryStream(preamble.Length + bytes.Length);
            stream.Write(preamble, 0, preamble.Length);
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;
            SetResponse(stream, mimeType: null, autoDisposeStream: false);
        }

        public void RespondWith(Stream stream, string extension = null) {
            SetResponse(stream, ResourcesManager.GetExtensionMimeType(extension), autoDisposeStream: false);
        }

        public void Redirect(string url) {
            lock (syncRoot) {
                RedirectUrl = url;
            }
        }

        private void SetResponse(Stream stream, string mimeType, bool autoDisposeStream) {
            lock (syncRoot) {
                Response = stream;
                MimeType = mimeType;
                AutoDisposeStream = autoDisposeStream;
            }
        }
    }
}
