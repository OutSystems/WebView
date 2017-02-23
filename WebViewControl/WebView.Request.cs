using CefSharp;

namespace WebViewControl {

    partial class WebView {

        public class Request {

            private readonly IRequest CefRequest;

            internal Request(IRequest request) {
                CefRequest = request;
            }

            public string Method {
                get { return CefRequest.Method; }
            }

            public string Url {
                get { return CefRequest.Url; }
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
