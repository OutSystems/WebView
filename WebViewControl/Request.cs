using CefSharp;

namespace WebViewControl {

    public class Request {

        private IRequest CefRequest { get; }
        private string UrlOverride { get; }

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
