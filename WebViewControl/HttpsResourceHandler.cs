using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    internal class HttpsResourceHandler : DefaultResourceHandler {

        internal static readonly CefResourceType[] AcceptedResources = new CefResourceType[] {
            CefResourceType.SubFrame,
            CefResourceType.FontResource
        };

        protected override RequestHandlingFashion ProcessRequestAsync(CefRequest request, CefCallback callback) {
            Task.Run(() => {
                var httpRequest = WebRequest.CreateHttp(request.Url);
                var headers = request.GetHeaderMap();
                foreach (var key in request.GetHeaderMap().AllKeys) {
                    httpRequest.Headers.Add(key, headers[key]);
                }
                httpRequest.GetResponseAsync().ContinueWith(r => {
                    Response = r.Result.GetResponseStream();
                    Headers = r.Result.Headers;
                    Headers.Add("Access-Control-Allow-Origin", "*");
                    callback.Continue();
                });
            });
            return RequestHandlingFashion.ContinueAsync;
        }

        protected override bool Read(Stream outResponse, int bytesToRead, out int bytesRead, CefResourceReadCallback callback) {
            var buffer = new byte[bytesToRead];
            bytesRead = Response.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0) {
                return false;
            }

            outResponse.Write(buffer, 0, bytesRead);
            return bytesRead > 0;
        }
    }
}
