using System;
using System.IO;
using System.Threading.Tasks;
using Xilium.CefGlue;

namespace WebViewControl {

    public sealed class ResourceHandler : Request {

        private bool isAsync;

        private readonly object syncRoot = new object();

        internal ResourceHandler(CefRequest request, string urlOverride)
            : base(request, urlOverride) {
        }

        internal AsyncResourceHandler Handler { get; private set; }

        public bool Handled { get; private set; }

        public Stream Response => Handler?.Response;

        private AsyncResourceHandler GetOrCreateCefResourceHandler() {
            if (Handler != null) {
                return Handler;
            }

            lock (syncRoot) {
                if (Handler != null) {
                    return Handler;
                }

                var handler = new AsyncResourceHandler();
                handler.Headers.Add("cache-control", "public, max-age=315360000");
                Handler = handler;
                return handler;
            }
        }

        public void BeginAsyncResponse(Action handleResponse) {
            isAsync = true;
            var handler = GetOrCreateCefResourceHandler();
            Task.Run(() => {
                handleResponse();
                handler.Continue();
            });
        }

        private void Continue() {
            var handler = Handler;
            Handled = handler != null && (handler.Response != null || !string.IsNullOrEmpty(handler.RedirectUrl));
            if (isAsync || handler == null) {
                return;
            }
            handler.Continue();
        }

        public void RespondWith(string filename) {
            var fileStream = File.OpenRead(filename);
            GetOrCreateCefResourceHandler().SetResponse(fileStream, ResourcesManager.GetMimeType(filename), autoDisposeStream: true);
            Continue();
        }

        public void RespondWithText(string text) {
            GetOrCreateCefResourceHandler().SetResponse(text);
            Continue();
        }

        public void RespondWith(Stream stream, string extension = null) {
            GetOrCreateCefResourceHandler().SetResponse(stream, ResourcesManager.GetExtensionMimeType(extension));
            Continue();
        }

        public void Redirect(string url) {
            GetOrCreateCefResourceHandler().RedirectTo(url);
            Continue();
        }
    }
}
