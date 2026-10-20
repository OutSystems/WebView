using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WebViewControl {

    /// <summary>
    /// A request made by a native engine (navigation or resource).
    /// </summary>
    internal sealed class NativeRequestDetails : IRequestDetails {

        private NameValueCollection headers;

        public NativeRequestDetails(string url, string method, ResourceType resourceType, NameValueCollection headers = null) {
            Url = url;
            Method = string.IsNullOrEmpty(method) ? "GET" : method;
            ResourceType = resourceType;
            this.headers = headers ?? new NameValueCollection(StringComparer.OrdinalIgnoreCase);
        }

        public string Method { get; }

        public string Url { get; }

        public ResourceType ResourceType { get; }

        public NameValueCollection GetHeaderMap() => new NameValueCollection(headers);

        // the native engines don't let the request headers be changed, the changes are only visible to the handlers
        public void SetHeaderMap(NameValueCollection headers) => this.headers = new NameValueCollection(headers);

        public string GetHeaderByName(string name) => headers[name];

        public void SetHeaderByName(string name, string value, bool overwrite) {
            if (overwrite || headers[name] == null) {
                headers[name] = value;
            }
        }
    }

    /// <summary>
    /// Collects the response given by the resource handlers, until <see cref="Continue"/> is called.
    /// </summary>
    internal sealed class NativeResourceResponse : IResourceResponse {

        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object syncRoot = new();
        private bool autoDisposeStream;
        private string mimeType;

        public Stream Response { get; private set; }

        public string RedirectUrl { get; private set; }

        public Task Completion => completion.Task;

        public void SetResponse(string response, string mimeType = null) {
            var encoding = Encoding.UTF8;
            var bytes = encoding.GetPreamble();
            var content = encoding.GetBytes(response ?? "");
            var stream = new MemoryStream(bytes.Length + content.Length);
            stream.Write(bytes, 0, bytes.Length);
            stream.Write(content, 0, content.Length);
            stream.Position = 0;
            SetResponse(stream, mimeType, autoDisposeStream: true);
        }

        public void SetResponse(Stream response, string mimeType = null, bool autoDisposeStream = false) {
            lock (syncRoot) {
                Response = response;
                this.mimeType = mimeType;
                this.autoDisposeStream = autoDisposeStream;
            }
        }

        public void RedirectTo(string targetUrl) {
            lock (syncRoot) {
                RedirectUrl = targetUrl;
            }
        }

        public void Continue() => completion.TrySetResult(true);

        /// <summary>
        /// Reads the response. Must be called after <see cref="Completion"/>.
        /// </summary>
        public NativeResourceResult ToResult(string url) {
            lock (syncRoot) {
                if (!string.IsNullOrEmpty(RedirectUrl)) {
                    return NativeResourceResult.Redirect(RedirectUrl);
                }

                var stream = Response;
                if (stream == null) {
                    return NativeResourceResult.NotFound;
                }

                try {
                    using (var content = new MemoryStream()) {
                        stream.CopyTo(content);
                        var resultMimeType = string.IsNullOrEmpty(mimeType) ? ResourcesManager.GetMimeType(GetPath(url)) : mimeType;
                        return NativeResourceResult.Ok(content.ToArray(), resultMimeType);
                    }
                } finally {
                    if (autoDisposeStream) {
                        stream.Dispose();
                    }
                }
            }
        }

        private static string GetPath(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
    }

    /// <summary>
    /// The response sent to a native engine.
    /// </summary>
    internal sealed class NativeResourceResult {

        private const string CacheControl = "public, max-age=315360000";

        private NativeResourceResult(int statusCode, string reasonPhrase, byte[] content, string mimeType, string redirectUrl) {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Content = content ?? Array.Empty<byte>();
            MimeType = string.IsNullOrEmpty(mimeType) ? "application/octet-stream" : mimeType;
            RedirectUrl = redirectUrl;
        }

        public static NativeResourceResult NotFound { get; } = new(404, "Not Found", null, "text/plain", null);

        public static NativeResourceResult Ok(byte[] content, string mimeType) => new(200, "OK", content, mimeType, null);

        public static NativeResourceResult Text(string text, string mimeType) {
            var encoding = Encoding.UTF8;
            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(text ?? "");
            var bytes = new byte[preamble.Length + content.Length];
            preamble.CopyTo(bytes, 0);
            content.CopyTo(bytes, preamble.Length);
            return Ok(bytes, mimeType);
        }

        public static NativeResourceResult Redirect(string url) => new(302, "Found", null, "text/plain", url);

        public int StatusCode { get; }

        public string ReasonPhrase { get; }

        public byte[] Content { get; }

        public string MimeType { get; }

        public string RedirectUrl { get; }

        /// <summary>
        /// The value of the Content-Type header: text is assumed to be utf-8 when the charset is not specified.
        /// </summary>
        public string ContentType {
            get {
                var isText = MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                             MimeType.EndsWith("javascript", StringComparison.OrdinalIgnoreCase) ||
                             MimeType.EndsWith("json", StringComparison.OrdinalIgnoreCase) ||
                             MimeType.EndsWith("xml", StringComparison.OrdinalIgnoreCase);
                return isText && MimeType.IndexOf("charset", StringComparison.OrdinalIgnoreCase) < 0 ? MimeType + "; charset=utf-8" : MimeType;
            }
        }

        public NameValueCollection GetHeaders() {
            var headers = new NameValueCollection {
                ["Content-Type"] = ContentType,
                ["Content-Length"] = Content.Length.ToString(),
                // resources of the custom schemes are fetched across schemes (eg: fonts and modules), which requires cors
                ["Access-Control-Allow-Origin"] = "*"
            };
            if (StatusCode == 200) {
                headers["Cache-Control"] = CacheControl;
            }
            if (!string.IsNullOrEmpty(RedirectUrl)) {
                headers["Location"] = RedirectUrl;
            }
            return headers;
        }
    }
}
