using System.IO;
using System.Text;
using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        public sealed class ResourceHandler : Request {

            internal ResourceHandler(IRequest request, string urlOverride)
                : base(request, urlOverride) {
            }

            public void RespondWith(string filename) {
                var handler = (CefSharp.ResourceHandler) CefSharp.ResourceHandler.FromFilePath(filename, CefSharp.ResourceHandler.GetMimeType(Path.GetExtension(filename)));
                handler.AutoDisposeStream = true;
                AddCacheInfo(handler);
                Handler = handler;
            }

            public void RespondWith(Stream stream, string extension) {
                var handler = CefSharp.ResourceHandler.FromStream(stream, CefSharp.ResourceHandler.GetMimeType(extension));
                AddCacheInfo(handler);
                Handler = handler;
            }

            private static void AddCacheInfo(CefSharp.ResourceHandler handler) {
                handler.Headers.Add("cache-control", "public, max-age=315360000");
            }

            public static IResourceHandler FromString(string content) {
                return CefSharp.ResourceHandler.FromString(content, Encoding.UTF8);
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
