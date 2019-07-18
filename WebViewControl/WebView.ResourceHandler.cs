using System;
using System.IO;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        public sealed class ResourceHandler : Request {

            private bool isAsync;

            internal ResourceHandler(CefRequest request, string urlOverride)
                : base(request, urlOverride) {
            }

            public void BeginAsyncResponse(Action handleResponse) {
                isAsync = true;
                if (Handler == null) {
                    Handler = CreateCefResourceHandler();
                }
                Task.Run(() => {
                    handleResponse();
                    Handler.Continue();
                });
            }

            private void Continue() {
                if (isAsync) {
                    return;
                }
                Handler.Continue();
            }

            private static CefAsyncResourceHandler CreateCefResourceHandler() {
                var handler = new CefAsyncResourceHandler();
                handler.Headers.Add("cache-control", "public, max-age=315360000");
                return handler;
            }

            public void RespondWith(string filename) {
                var fileStream = File.OpenRead(filename);
                if (Handler == null) {
                    Handler = CreateCefResourceHandler();
                }
                Handler.SetResponse(fileStream, ResourcesManager.GetMimeType(filename) /* TODO, autoDisposeStream: true*/);
                Continue();
            }

            public void RespondWithText(string text) {
                if (Handler == null) {
                    Handler = CreateCefResourceHandler();
                }
                Handler.SetResponse(text);
                Continue();
            }

            public void RespondWith(Stream stream, string extension = null) {
                if (Handler == null) {
                    Handler = CreateCefResourceHandler();
                }
                Handler.SetResponse(stream, ResourcesManager.GetExtensionMimeType(extension));
                Continue();
            }

            public void Redirect(string url) {
                if (Handler == null) {
                    Handler = CreateCefResourceHandler();
                }
                Handler.RedirectTo(url);
                Continue();
            }

            internal CefAsyncResourceHandler Handler { get; private set; }

            public bool Handled => Handler != null;

            public Stream Response => (Handler as DefaultResourceHandler)?.Response;
        }
    }
}
