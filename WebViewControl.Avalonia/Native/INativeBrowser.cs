using System;
using System.Collections.Specialized;
using System.IO;
using Avalonia.Platform;

namespace WebViewControl.Native {

    internal enum JavascriptDialogKind {
        Alert,
        Confirm,
        Prompt
    }

    /// <summary>
    /// Response for a resource requested to the web view (through one of the custom schemes).
    /// </summary>
    internal sealed class ResourceResponse {
        public int StatusCode { get; set; } = 200;
        public string MimeType { get; set; }
        public NameValueCollection Headers { get; set; }
        public Stream Content { get; set; }
        public bool AutoDisposeContent { get; set; }

        /// <summary>
        /// Reads the whole content, always from its start: applications can serve the same (seekable) stream instance to several requests
        /// (e.g. cached images), and the cef based web view always read the streams from the beginning.
        /// Concurrent requests that share a stream are serialized.
        /// </summary>
        public byte[] ReadContent() {
            var stream = Content;
            if (stream == null) {
                return Array.Empty<byte>();
            }

            try {
                lock (stream) {
                    if (stream.CanSeek) {
                        stream.Position = 0;
                    }
                    if (stream is MemoryStream memoryStream) {
                        return memoryStream.ToArray();
                    }
                    using (var buffer = new MemoryStream()) {
                        stream.CopyTo(buffer);
                        return buffer.ToArray();
                    }
                }
            } finally {
                if (AutoDisposeContent) {
                    stream.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// A resource request coming from the native web view. The request must be completed exactly once, from any thread.
    /// </summary>
    internal abstract class ResourceRequest {
        public string Url { get; protected set; }
        public string Method { get; protected set; }
        public NameValueCollection Headers { get; protected set; }

        /// <summary>
        /// Completes the request. A null response completes the request with a 404.
        /// </summary>
        public abstract void Complete(ResourceResponse response);
    }

    /// <summary>
    /// Implemented by the <see cref="WebView"/>. The native browser calls it back on the UI thread.
    /// </summary>
    internal interface INativeBrowserOwner {
        /// <returns>True to cancel the navigation</returns>
        bool OnNavigating(string url, string method, bool isMainFrame, bool isBackForward);
        void OnNavigationCommitted(string url);
        void OnNavigationFinished(string url);
        void OnNavigationFailed(string url, int errorCode, string description);
        /// <summary>
        /// Message posted by the page using the native bridge (see NativeBridge.js).
        /// </summary>
        void OnScriptMessage(string json);
        void OnResourceRequested(ResourceRequest request);
        void OnTitleChanged(string title);
        void OnAddressChanged(string url);
        void OnProcessTerminated();
        /// <returns>True when the popup request was handled</returns>
        bool OnPopupRequested(string url);
        /// <returns>True when the dialog was handled by the owner (it must call <paramref name="complete"/> later), false to use the default behavior</returns>
        bool OnJavascriptDialog(JavascriptDialogKind kind, string message, string defaultValue, Action<bool, string> complete);
        /// <summary>
        /// The native web view was created (and <see cref="INativeBrowser.IsCreated"/> is now true).
        /// </summary>
        void OnBrowserInitialized();
        void OnError(Exception exception);

        /// <summary>
        /// The server (or the proxy) requires credentials.
        /// </summary>
        /// <returns>True when credentials are provided</returns>
        bool OnAuthenticationRequested(bool isProxy, string host, int port, string realm, out string userName, out string password);

        /// <returns>True to load the page even though the server certificate is not trusted</returns>
        bool OnCertificateError(string host);
    }

    /// <summary>
    /// Abstraction over the operating system native web view. All members must be called in the UI thread.
    /// </summary>
    internal interface INativeBrowser : IDisposable {

        /// <summary>
        /// True when the native web view is ready to be used.
        /// </summary>
        bool IsCreated { get; }

        /// <summary>
        /// Starts creating the underlying native web view, if not created yet. The creation can be asynchronous, in that case
        /// <see cref="INativeBrowserOwner.OnBrowserInitialized"/> is called when it completes.
        /// </summary>
        void Create(int width, int height);

        /// <summary>
        /// Called when the size of the control hosting the native view changes (in device independent units).
        /// </summary>
        void OnHostBoundsChanged(double width, double height, double scaling);

        /// <summary>
        /// Called when the native view stops being hosted by the avalonia control (its window was closed, the view was removed...).
        /// </summary>
        void OnHostDetached();

        /// <summary>
        /// Gets the handle to be hosted by the avalonia native control host.
        /// </summary>
        IPlatformHandle CreatePlatformHandle(IPlatformHandle parent);

        string Address { get; }
        string Title { get; }
        bool CanGoBack { get; }
        bool CanGoForward { get; }
        bool IsLoading { get; }

        void LoadUrl(string url);
        void Reload(bool ignoreCache);
        void GoBack();
        void GoForward();

        /// <summary>
        /// Runs the script in the main frame without waiting for the result.
        /// </summary>
        void RunScript(string script);

        double ZoomFactor { get; set; }

        bool AreDeveloperToolsAllowed { get; set; }

        /// <summary>
        /// Disables the same origin policy (cross origin requests are not blocked, canvases are not tainted).
        /// Only applied when the native web view is created.
        /// </summary>
        bool IsWebSecurityDisabled { get; set; }
        void ShowDeveloperTools();
        void CloseDeveloperTools();

        void Focus();

        void Cut();
        void Copy();
        void Paste();
        void SelectAll();
    }
}
