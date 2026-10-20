using System.Collections.Specialized;

namespace WebViewControl {

    public class Request {

        private IRequestDetails RequestDetails { get; }
        private string UrlOverride { get; }

        internal Request(IRequestDetails request, string urlOverride) {
            RequestDetails = request;
            UrlOverride = urlOverride;
        }

        public string Method {
            get { return RequestDetails.Method; }
        }

        public string Url {
            get { return UrlOverride ?? RequestDetails.Url; }
        }

        public virtual void Cancel() {
            Canceled = true;
        }

        public bool Canceled { get; private set; }

        internal bool IsMainFrame => RequestDetails.ResourceType == ResourceType.MainFrame;

        public NameValueCollection GetHeaderMap() =>
            RequestDetails.GetHeaderMap();

        public void SetHeaderMap(NameValueCollection headers) =>
            RequestDetails.SetHeaderMap(headers);

        public string GetHeaderByName(string name) => 
            RequestDetails.GetHeaderByName(name);

        public void SetHeaderByName(string name, string value, bool overwrite) => 
            RequestDetails.SetHeaderByName(name, value, overwrite);
    }
}