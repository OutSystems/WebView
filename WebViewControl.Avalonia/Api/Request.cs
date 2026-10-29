using System.Collections.Specialized;

namespace WebViewControl {

    public class Request {

        private NameValueCollection headers;
        private readonly string url;
        private readonly string urlOverride;

        internal Request(string method, string url, string urlOverride, NameValueCollection headers, bool isMainFrame) {
            Method = method;
            this.url = url;
            this.urlOverride = urlOverride;
            this.headers = headers ?? new NameValueCollection();
            IsMainFrame = isMainFrame;
        }

        public string Method { get; }

        public string Url => urlOverride ?? url;

        public virtual void Cancel() {
            Canceled = true;
        }

        public bool Canceled { get; private set; }

        internal bool IsMainFrame { get; }

        // NOTE: native web views do not allow changing the headers of a request that is being loaded,
        // the setters are kept for API compatibility and only change the copy exposed to the handlers.

        public NameValueCollection GetHeaderMap() => new NameValueCollection(headers);

        public void SetHeaderMap(NameValueCollection headerMap) => headers = new NameValueCollection(headerMap ?? new NameValueCollection());

        public string GetHeaderByName(string name) => headers[name] ?? string.Empty;

        public void SetHeaderByName(string name, string value, bool overwrite) {
            if (overwrite || headers[name] == null) {
                headers[name] = value;
            }
        }
    }
}
