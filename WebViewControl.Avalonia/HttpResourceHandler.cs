using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    internal class HttpResourceHandler : DefaultResourceHandler {

        private const string AccessControlAllowOriginHeaderKey = "Access-Control-Allow-Origin";

        internal static readonly CefResourceType[] AcceptedResources = new CefResourceType[] {
            // These resources types need an "Access-Control-Allow-Origin" header response entry
            // to comply with CORS security restrictions.
            CefResourceType.SubFrame,
            CefResourceType.FontResource,
            CefResourceType.Stylesheet
        };

        protected override RequestHandlingFashion ProcessRequestAsync(CefRequest request, CefCallback callback) {
            Task.Run(async () => {
                try {
                    var httpRequest = WebRequest.CreateHttp(request.Url);
                    var headers = request.GetHeaderMap();
                    foreach (var key in headers.AllKeys) {
                        httpRequest.Headers.Add(key, headers[key]);
                    }

                    var response = (HttpWebResponse) await httpRequest.GetResponseAsync();
                    Response = response.GetResponseStream();
                    Headers = response.Headers;

                    MimeType = response.ContentType;
                    Status = (int) response.StatusCode;
                    StatusText = response.StatusDescription;

                    // we have to smash any existing value here
                    Headers.Remove(AccessControlAllowOriginHeaderKey);
                    Headers.Add(AccessControlAllowOriginHeaderKey, "*");

                } catch {
                    // we should catch exceptions.. network errors cannot crash the app
                } finally {
                    callback.Continue();
                }

            });
            return RequestHandlingFashion.ContinueAsync;       
        }

        protected override bool Read(Stream outResponse, int bytesToRead, out int bytesRead, CefResourceReadCallback callback) {
            var buffer = new byte[bytesToRead];
            bytesRead = Response?.Read(buffer, 0, buffer.Length) ?? 0;

            if (bytesRead == 0) {
                return false;
            }

            outResponse.Write(buffer, 0, bytesRead);
            return bytesRead > 0;
        }
    }
}
