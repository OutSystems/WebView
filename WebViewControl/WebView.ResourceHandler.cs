using System.IO;
using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        public sealed class ResourceHandler : Request {

            internal ResourceHandler(IRequest request, string urlOverride)
                : base(request, urlOverride) {
            }

            public void RespondWith(string filename) {
                var resourceHandler = (CefSharp.ResourceHandler) CefSharp.ResourceHandler.FromFilePath(filename, CefSharp.ResourceHandler.GetMimeType(Path.GetExtension(filename)));
                resourceHandler.AutoDisposeStream = true;
                Handler = resourceHandler;
            }

            public void RespondWith(Stream stream, string extension) {
                Handler = CefSharp.ResourceHandler.FromStream(stream, CefSharp.ResourceHandler.GetMimeType(extension));
            }

            public void Redirect(string url) {
                Handler = new CefResourceHandler(url);
            }

            internal IResourceHandler Handler {
                get;
                private set;
            }

            public bool Handled {
                get { return Handler != null; }
            }

            public Stream Response {
                get {
                    var handler = Handler as CefSharp.ResourceHandler;
                    return handler != null ? handler.Stream : null;
                }
            }
        }
    }
}
