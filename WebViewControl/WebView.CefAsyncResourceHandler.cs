using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using CefSharp;
using CefSharp.Callback;

namespace WebViewControl {

    partial class WebView {

        internal class CefAsyncResourceHandler : IResourceHandler {

            private class CallbackWrapper : IResourceReadCallback {

                private readonly ICallback callback;

                public CallbackWrapper(ICallback callback) {
                    this.callback = callback;
                }
                bool IResourceReadCallback.IsDisposed => callback.IsDisposed;

                void IResourceReadCallback.Continue(int bytesRead) {
                    callback.Continue();
                }

                void IDisposable.Dispose() {
                    callback.Dispose();
                }
            }

            private readonly CefSharp.ResourceHandler resourceHandler = new CefSharp.ResourceHandler();  
            private ICallback responseCallback;
            private string redirectUrl;
            private bool hasStreamBeenSet;

            internal string RedirectUrl => redirectUrl;

            public Stream Stream => resourceHandler.Stream;
            public NameValueCollection Headers => resourceHandler.Headers;

            public bool Handled => hasStreamBeenSet || !string.IsNullOrEmpty(redirectUrl);

            public void SetResponse(Stream response, string mimeType = CefSharp.ResourceHandler.DefaultMimeType, bool autoDisposeStream = false) {
                hasStreamBeenSet = response != null;
                if (response?.CanSeek == true) {
                    // move stream to the beginning
                    response.Position = 0;
                }
                resourceHandler.Stream = response;
                resourceHandler.MimeType = mimeType;
                resourceHandler.AutoDisposeStream = autoDisposeStream;
            }

            public void SetResponse(string response) {
                SetResponse(CefSharp.ResourceHandler.GetMemoryStream(response, Encoding.UTF8, false), autoDisposeStream: true);
            }

            public void RedirectTo(string targetUrl) {
                redirectUrl = targetUrl;
            }

            public void Continue() {
                if (responseCallback != null) {
                    using (responseCallback) {
                        responseCallback.Continue();
                    }
                }
            }

            private CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback) {
                if (resourceHandler.Stream == null && string.IsNullOrEmpty(redirectUrl)) {
                    responseCallback = callback;
                    return CefReturnValue.ContinueAsync;
                }
                return CefReturnValue.Continue;
            }

            bool IResourceHandler.ProcessRequest(IRequest request, ICallback callback) {
                var processRequest = ProcessRequestAsync(request, callback);
                if (processRequest == CefReturnValue.Continue) {
                    callback.Continue();
                }
                return processRequest != CefReturnValue.Cancel;
            }

            bool IResourceHandler.ReadResponse(Stream dataOut, out int bytesRead, ICallback callback) {
                 return ResourceHandler.Read(dataOut, out bytesRead, new CallbackWrapper(callback));
            }

            void IResourceHandler.GetResponseHeaders(IResponse response, out long responseLength, out string redirectUrl) {
                redirectUrl = this.redirectUrl;
                ResourceHandler.GetResponseHeaders(response, out responseLength, out _);
            }

            bool IResourceHandler.Open(IRequest request, out bool handleRequest, ICallback callback) {
                var processRequest = ProcessRequestAsync(request, callback);

                //Process the request in an async fashion
                switch (processRequest) {
                    case CefReturnValue.ContinueAsync:
                        handleRequest = false;
                        return true;
                    case CefReturnValue.Continue:
                        handleRequest = true;
                        return true;
                }

                //Cancel Request
                handleRequest = true;

                return false;
            }

            bool IResourceHandler.Skip(long bytesToSkip, out long bytesSkipped, IResourceSkipCallback callback) {
                return ResourceHandler.Skip(bytesToSkip, out bytesSkipped, callback);
            }

            bool IResourceHandler.Read(Stream dataOut, out int bytesRead, IResourceReadCallback callback) {
                return ResourceHandler.Read(dataOut, out bytesRead, callback);
            }

            void IResourceHandler.Cancel() {
                ResourceHandler.Cancel();
            }

            void IDisposable.Dispose() {
                ResourceHandler.Dispose();
            }

            private IResourceHandler ResourceHandler => (IResourceHandler)resourceHandler;
        }
    }
}
