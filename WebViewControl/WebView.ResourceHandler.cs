using System.IO;
using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        public sealed class ResourceHandler : Request {

            internal ResourceHandler(IRequest request)
                : base(request) {
            }

            public void RespondWith(string filename) {
                Handler = CefSharp.ResourceHandler.FromFilePath(filename);
            }

            public void RespondWith(Stream stream, string extension) {
                Handler = CefSharp.ResourceHandler.FromStream(stream, CefSharp.ResourceHandler.GetMimeType(extension));
            }

            internal CefSharp.ResourceHandler Handler {
                get;
                private set;
            }

            public bool Handled {
                get { return Handler != null; }
            }
        }
    }
}
