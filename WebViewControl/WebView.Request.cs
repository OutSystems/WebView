using CefSharp;

namespace WebViewControl {

    partial class WebView {

        public class Request {

            private readonly IRequest CefRequest;
            private readonly string UrlOverride;

            internal Request(IRequest request, string urlOverride) {
                CefRequest = request;
                UrlOverride = urlOverride;
            }

            public string Method {
                get { return CefRequest.Method; }
            }

            public string Url {
                get { return UrlOverride ?? CefRequest.Url; }
            }

            public virtual void Cancel() {
                Canceled = true;
            }

            public bool Canceled {
                get;
                private set;
            }
        }
    }
}
