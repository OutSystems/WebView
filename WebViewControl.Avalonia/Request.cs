using System.Collections.Specialized;
using Xilium.CefGlue;

namespace WebViewControl {

    public class Request {

        private CefRequest CefRequest { get; }
        private string UrlOverride { get; }

        internal Request(CefRequest request, string urlOverride) {
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

        public bool Canceled { get; private set; }

        internal bool IsMainFrame => CefRequest.ResourceType == CefResourceType.MainFrame;

        public NameValueCollection GetHeaderMap() =>
            CefRequest.GetHeaderMap();

        public void SetHeaderMap(NameValueCollection headers) =>
            CefRequest.SetHeaderMap(headers);

        public string GetHeaderByName(string name) => 
            CefRequest.GetHeaderByName(name);

        public void SetHeaderByName(string name, string value, bool overwrite) => 
            CefRequest.SetHeaderByName(name, value, overwrite);
    }
}