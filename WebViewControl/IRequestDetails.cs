using System.Collections.Specialized;
using System.IO;

namespace WebViewControl {

    /// <summary>
    /// Engine agnostic view over a request being made by the browser.
    /// </summary>
    internal interface IRequestDetails {

        string Method { get; }

        string Url { get; }

        ResourceType ResourceType { get; }

        NameValueCollection GetHeaderMap();

        void SetHeaderMap(NameValueCollection headers);

        string GetHeaderByName(string name);

        void SetHeaderByName(string name, string value, bool overwrite);
    }

    /// <summary>
    /// Engine agnostic sink for the response of an intercepted resource request.
    /// </summary>
    internal interface IResourceResponse {

        Stream Response { get; }

        string RedirectUrl { get; }

        void SetResponse(string response, string mimeType = null);

        void SetResponse(Stream response, string mimeType = null, bool autoDisposeStream = false);

        void RedirectTo(string targetUrl);

        /// <summary>
        /// Signals that the response is ready to be sent to the browser.
        /// </summary>
        void Continue();
    }
}
