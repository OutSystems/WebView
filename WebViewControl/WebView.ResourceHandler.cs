using System;
using System.IO;
using System.Threading.Tasks;

namespace WebViewControl {

    public sealed class ResourceHandler : Request {

        private bool isAsync;

        private readonly object syncRoot = new object();

        private readonly Func<IResourceResponse> responseFactory;

        internal ResourceHandler(IRequestDetails request, string urlOverride, Func<IResourceResponse> responseFactory)
            : base(request, urlOverride) {
            this.responseFactory = responseFactory;
        }

        internal IResourceResponse Handler { get; private set; }

        public bool Handled { get; private set; }

        public Stream Response => Handler?.Response;

        private IResourceResponse GetOrCreateResponse() {
            if (Handler != null) {
                return Handler;
            }

            lock (syncRoot) {
                if (Handler != null) {
                    return Handler;
                }

                var handler = responseFactory();
                Handler = handler;
                return handler;
            }
        }

        public void BeginAsyncResponse(Action handleResponse) {
            isAsync = true;
            var handler = GetOrCreateResponse();
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
            GetOrCreateResponse().SetResponse(fileStream, ResourcesManager.GetMimeType(filename), autoDisposeStream: true);
            Continue();
        }

        public void RespondWithText(string text) {
            GetOrCreateResponse().SetResponse(text);
            Continue();
        }

        public void RespondWith(Stream stream, string extension = null) {
            GetOrCreateResponse().SetResponse(stream, ResourcesManager.GetExtensionMimeType(extension));
            Continue();
        }

        public void Redirect(string url) {
            GetOrCreateResponse().RedirectTo(url);
            Continue();
        }
    }
}
