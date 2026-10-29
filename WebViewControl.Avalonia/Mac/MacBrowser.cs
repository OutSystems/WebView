using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using Avalonia.Platform;
using Avalonia.Threading;
using static WebViewControl.Native.Mac.Objc;

namespace WebViewControl.Native.Mac {

    /// <summary>
    /// Completes a request received by the WKURLSchemeHandler.
    /// </summary>
    internal sealed class MacResourceRequest : ResourceRequest {

        private readonly MacBrowser browser;
        private int completed;

        public MacResourceRequest(MacBrowser browser, IntPtr task, string url, string method, NameValueCollection headers) {
            this.browser = browser;
            Task = task;
            Url = url;
            Method = method;
            Headers = headers;
        }

        /// <summary>
        /// The (retained) WKURLSchemeTask.
        /// </summary>
        public IntPtr Task { get; }

        /// <summary>
        /// Set (in the UI thread) when WebKit cancelled the task, from then on no message can be sent to it.
        /// </summary>
        public bool Stopped { get; set; }

        public override void Complete(ResourceResponse response) {
            if (Interlocked.Exchange(ref completed, 1) != 0) {
                return;
            }

            // read the content in the caller thread, it can be slow
            byte[] content = Array.Empty<byte>();
            try {
                if (response != null) {
                    content = response.ReadContent();
                }
            } catch (Exception e) {
                browser.ReportError(e);
                response = null;
            }

            browser.RunOnUIThread(() => browser.SendSchemeTaskResponse(this, response, content));
        }
    }

    internal sealed class MacBrowser : INativeBrowser {

        private const string BridgeHandlerName = "rvBridge";

        private static IntPtr sharedNonPersistentDataStore;

        private readonly INativeBrowserOwner owner;
        private readonly Dictionary<IntPtr, MacResourceRequest> activeTasks = new Dictionary<IntPtr, MacResourceRequest>();

        private IntPtr bridge;
        private IntPtr userContentController;
        private IntPtr webView;

        // The view that avalonia hosts. The web view is inside it, so its size is not tied to the size of the host:
        // WebKit does not render a view with an empty frame, and the host is empty until the page reports the size of its content.
        private IntPtr hostView;
        private int preloadWidth = 800;
        private int preloadHeight = 600;
        private double hostWidth;
        private double hostHeight;
        private bool isHostAttached;
        private bool disposed;
        private bool allowDeveloperTools;
        private string lastTitle;

        static MacBrowser() {
            if (NativeTrace.Enabled) {
                // the windows of the application, every 4 seconds
                DispatcherTimer.Run(() => {
                    TraceWindows("periodic");
                    return true;
                }, TimeSpan.FromSeconds(4));
            }
        }

        public MacBrowser(INativeBrowserOwner owner) {
            this.owner = owner;
        }

        public bool IsCreated => webView != IntPtr.Zero;

        // ---- creation ------------------------------------------------------------------------------------------------

        public void Create(int width, int height) {
            if (webView != IntPtr.Zero || disposed) {
                return;
            }

            var settings = WebView.Settings;

            preloadWidth = Math.Max(1, width);
            preloadHeight = Math.Max(1, height);

            bridge = MacBridgeClass.CreateInstance(this);

            var configuration = New("WKWebViewConfiguration");
            try {
                Send(configuration, Sel("setWebsiteDataStore:"), GetDataStore(settings.PersistCache));

                foreach (var scheme in new[] { ResourceUrl.EmbeddedScheme, ResourceUrl.CustomScheme, ResourceUrl.LocalScheme }) {
                    Send(configuration, Sel("setURLSchemeHandler:forURLScheme:"), bridge, NSString(scheme));
                }

                if (settings.EnableVideoAutoplay) {
                    Send(configuration, Sel("setMediaTypesRequiringUserActionForPlayback:"), 0L); // WKAudiovisualMediaTypeNone
                }

                userContentController = Retain(Send(configuration, Sel("userContentController")));
                Send(userContentController, Sel("addScriptMessageHandler:name:"), bridge, NSString(BridgeHandlerName));

                // WKUserScriptInjectionTimeAtDocumentStart = 0, main frame only (every frame while tracing: TEMPORARY, for the drag and drop tracing in NativeBridge.js)
                var userScript = Send(Send(Cls("WKUserScript"), Sel("alloc")), Sel("initWithSource:injectionTime:forMainFrameOnly:"), NSString(NativeBridgeScript.Source), 0L, NativeTrace.Enabled ? (byte)0 : (byte)1);
                Send(userContentController, Sel("addUserScript:"), userScript);
                Release(userScript);

                // the web view works on a copy of the configuration, the preferences have to be set before it is created
                var preferences = Send(configuration, Sel("preferences"));
                SetDeveloperExtras(preferences, allowDeveloperTools);
                if (IsWebSecurityDisabled && RespondsTo(preferences, "_setWebSecurityEnabled:")) {
                    // same as the cef based web view with IsSecurityDisabled (--disable-web-security)
                    Send(preferences, Sel("_setWebSecurityEnabled:"), (byte)0);
                }

                webView = Send(Send(Cls("WKWebView"), Sel("alloc")), Sel("initWithFrame:configuration:"), new CGRect(0, 0, width, height), configuration);
            } finally {
                Release(configuration); // the web view keeps its own copy
            }

            Send(webView, Sel("setNavigationDelegate:"), bridge);
            Send(webView, Sel("setUIDelegate:"), bridge);

            if (!string.IsNullOrEmpty(settings.UserAgent)) {
                Send(webView, Sel("setCustomUserAgent:"), NSString(settings.UserAgent));
            }

            var color = settings.BackgroundColor;
            if (color.ToArgb() != System.Drawing.Color.White.ToArgb() && RespondsTo(webView, "setUnderPageBackgroundColor:")) {
                var nsColor = Send(Cls("NSColor"), Sel("colorWithSRGBRed:green:blue:alpha:"), color.R / 255.0, color.G / 255.0, color.B / 255.0, 1.0);
                Send(webView, Sel("setUnderPageBackgroundColor:"), nsColor);
            }

            ApplyInspectable(allowDeveloperTools);

            // the view is created before it is hosted (preload)
            UpdateWebViewParent();

            // notify asynchronously, like the other browser events
            Dispatcher.UIThread.Post(() => {
                if (!disposed) {
                    Guard(owner.OnBrowserInitialized);
                }
            });
        }

        public void OnHostDetached() {
            isHostAttached = false;
            if (!disposed) {
                UpdateWebViewParent();
            }
        }

        // ---- parking ------------------------------------------------------------------------------------------------

        private static IntPtr parkingWindow;

        /// <summary>
        /// WebKit only renders the pages of the views that are visible: a view that is not in a window is "hidden", so requestAnimationFrame never runs,
        /// the timers are throttled and the ResizeObserver notifications are not delivered. The views are created before they are hosted (preload) and
        /// the windows that host them wait for the page to report its layout (the size of the dialogs, of the tooltips) before being shown,
        /// so a view that is not hosted yet is kept in an invisible window (the chromium based web view always renders).
        /// The window is tiny, transparent, ignores the mouse and floats above the normal windows (otherwise the other windows would occlude it).
        /// </summary>
        private void Park() {
            var parkingContent = parkingWindow == IntPtr.Zero ? IntPtr.Zero : Send(parkingWindow, Sel("contentView"));
            if (parkingContent != IntPtr.Zero && Send(webView, Sel("superview")) == parkingContent) {
                return; // already parked
            }

            if (parkingWindow == IntPtr.Zero) {
                var screen = Send(Cls("NSScreen"), Sel("mainScreen"));
                var screenFrame = screen == IntPtr.Zero ? new CGRect(0, 0, 100, 100) : SendRect(screen, Sel("frame"));

                var window = Send(Send(Cls("NSWindow"), Sel("alloc")), Sel("initWithContentRect:styleMask:backing:defer:"),
                                  new CGRect(screenFrame.X + screenFrame.Width / 2, screenFrame.Y + screenFrame.Height / 2, 1, 1), 0L /* borderless */, 2L /* buffered */, (byte)0);
                Send(window, Sel("setReleasedWhenClosed:"), (byte)0);
                Send(window, Sel("setOpaque:"), (byte)0);
                Send(window, Sel("setBackgroundColor:"), Send(Cls("NSColor"), Sel("clearColor")));
                Send(window, Sel("setHasShadow:"), (byte)0);
                Send(window, Sel("setIgnoresMouseEvents:"), (byte)1);
                Send(window, Sel("setExcludedFromWindowsMenu:"), (byte)1);
                Send(window, Sel("setAlphaValue:"), 0.0);
                Send(window, Sel("setLevel:"), 3L); // NSFloatingWindowLevel
                Send(window, Sel("setCollectionBehavior:"), 1L | 8L | 16L | 64L); // all spaces, transient, stationary, ignored by the windows cycle
                Send(window, Sel("orderFrontRegardless"));
                parkingWindow = window;
            }

            NativeTrace.Write("parking the web view in the invisible window, so it keeps rendering until it is hosted");
            Send(Send(parkingWindow, Sel("contentView")), Sel("addSubview:"), webView);
        }

        public void OnHostBoundsChanged(double width, double height, double scaling) {
            hostWidth = width;
            hostHeight = height;
            UpdateWebViewParent();
            if (NativeTrace.Enabled && webView != IntPtr.Zero) {
                NativeTrace.Write($"host bounds {width:0.#}x{height:0.#}; web view frame={SendRect(webView, Sel("frame"))} window={DescribeWindow(Send(webView, Sel("window")))}");
            }
        }

        private static string DescribeWindow(IntPtr window) {
            if (window == IntPtr.Zero) {
                return "none";
            }
            var title = FromNSString(Send(window, Sel("title")));
            var cls = FromNSString(Send(Send(window, Sel("class")), Sel("description")));
            return $"'{title}' {cls} visible={SendBool(window, Sel("isVisible"))} key={SendBool(window, Sel("isKeyWindow"))} frame={SendRect(window, Sel("frame"))}";
        }

        /// <summary>
        /// Trace only: logs the windows of the application.
        /// </summary>
        internal static void TraceWindows(string reason) {
            var windows = Send(Send(Cls("NSApplication"), Sel("sharedApplication")), Sel("windows"));
            var count = SendLong(windows, Sel("count"));
            NativeTrace.Write($"windows ({reason}): {count}");
            for (long i = 0; i < count; i++) {
                var window = Send(windows, Sel("objectAtIndex:"), (IntPtr)i);
                NativeTrace.Write("   " + DescribeWindow(window));
                if (SendBool(window, Sel("isVisible")) != 0) {
                    TraceViews(Send(window, Sel("contentView")), 1);
                }
            }
        }

        private static void TraceViews(IntPtr view, int depth) {
            if (view == IntPtr.Zero || depth > 6) {
                return;
            }
            var cls = FromNSString(Send(Send(view, Sel("class")), Sel("description")));
            var extra = cls == "WKWebView" ? $" url={FromNSUrl(Send(view, Sel("URL")))} loading={SendBool(view, Sel("isLoading"))}" : "";
            NativeTrace.Write($"   {new string(' ', depth * 2)}{cls} frame={SendRect(view, Sel("frame"))} hidden={SendBool(view, Sel("isHidden"))}{extra}");
            var subviews = Send(view, Sel("subviews"));
            var count = SendLong(subviews, Sel("count"));
            for (long i = 0; i < count; i++) {
                TraceViews(Send(subviews, Sel("objectAtIndex:"), (IntPtr)i), depth + 1);
            }
        }

        public IPlatformHandle CreatePlatformHandle(IPlatformHandle parent) {
            Create(800, 600);
            NativeTrace.Write($"attaching native view to host view (window={DescribeWindow(Send(parent.Handle, Sel("window")))})");

            if (hostView == IntPtr.Zero) {
                hostView = New("NSView");
            }
            isHostAttached = true;
            UpdateWebViewParent();
            return new PlatformHandle(hostView, "NSView");
        }

        private bool HostHasSize => hostWidth >= 1 && hostHeight >= 1;

        /// <summary>
        /// The web view is inside the avalonia host while the host has a size, and has the size of the host. Otherwise (not hosted, or the host is
        /// still empty) it is parked and keeps the size it was preloaded with.
        /// Avalonia hides the native views of the hosts that are empty, and WebKit does not render a view that is hidden or has an empty frame.
        /// But the host is empty until the page reports the size of its content, which it can only do if it is rendered: so while the host is empty
        /// the page is laid out, rendered and can report the size of its content as if it was hosted (as the cef based web view did).
        /// </summary>
        private void UpdateWebViewParent() {
            if (webView == IntPtr.Zero) {
                return;
            }

            if (isHostAttached && HostHasSize) {
                if (Send(webView, Sel("superview")) != hostView) {
                    Send(hostView, Sel("addSubview:"), webView); // also takes it from the parking window
                }
                Send(webView, Sel("setFrame:"), new CGRect(0, 0, hostWidth, hostHeight));
            } else {
                Park();
                Send(webView, Sel("setFrame:"), new CGRect(0, 0, preloadWidth, preloadHeight));
            }
        }

        private static IntPtr GetDataStore(bool persist) {
            if (persist) {
                return Send(Cls("WKWebsiteDataStore"), Sel("defaultDataStore"));
            }
            // shared by all the web views, so the local storage is shared between views that use the same domain
            if (sharedNonPersistentDataStore == IntPtr.Zero) {
                sharedNonPersistentDataStore = Retain(Send(Cls("WKWebsiteDataStore"), Sel("nonPersistentDataStore")));
            }
            return sharedNonPersistentDataStore;
        }

        // ---- state ---------------------------------------------------------------------------------------------------

        public string Address => webView == IntPtr.Zero ? null : FromNSUrl(Send(webView, Sel("URL")));

        public string Title => webView == IntPtr.Zero ? null : FromNSString(Send(webView, Sel("title")));

        public bool CanGoBack => webView != IntPtr.Zero && SendBool(webView, Sel("canGoBack")) != 0;

        public bool CanGoForward => webView != IntPtr.Zero && SendBool(webView, Sel("canGoForward")) != 0;

        public bool IsLoading => webView != IntPtr.Zero && SendBool(webView, Sel("isLoading")) != 0;

        public double ZoomFactor {
            get => webView == IntPtr.Zero || !RespondsTo(webView, "pageZoom") ? 1.0 : SendDouble(webView, Sel("pageZoom"));
            set {
                if (webView != IntPtr.Zero && RespondsTo(webView, "setPageZoom:")) {
                    Send(webView, Sel("setPageZoom:"), value);
                }
            }
        }

        public bool IsWebSecurityDisabled { get; set; }

        public bool AreDeveloperToolsAllowed {
            get => allowDeveloperTools;
            set {
                allowDeveloperTools = value;
                ApplyInspectable(value);
            }
        }

        private void ApplyInspectable(bool inspectable) {
            if (webView == IntPtr.Zero) {
                return;
            }

            var flag = (byte)(inspectable ? 1 : 0);

            // WKWebView.isInspectable (macOS 13.3+) allows attaching the Safari web inspector (Develop menu)
            if (RespondsTo(webView, "setInspectable:")) {
                Send(webView, Sel("setInspectable:"), flag);
            }

            SetDeveloperExtras(Send(Send(webView, Sel("configuration")), Sel("preferences")), inspectable);
        }

        /// <summary>
        /// The local inspector (context menu "Inspect Element", <see cref="ShowDeveloperTools"/>) is gated by a (private) preference.
        /// </summary>
        private static void SetDeveloperExtras(IntPtr preferences, bool enabled) {
            if (RespondsTo(preferences, "_setDeveloperExtrasEnabled:")) {
                Send(preferences, Sel("_setDeveloperExtrasEnabled:"), (byte)(enabled ? 1 : 0));
            }
        }

        // ---- navigation ----------------------------------------------------------------------------------------------

        public void LoadUrl(string url) {
            if (webView == IntPtr.Zero) {
                return;
            }
            var nsUrl = NSUrl(url);
            if (nsUrl == IntPtr.Zero) {
                throw new ArgumentException($"Invalid url: {url}", nameof(url));
            }
            Send(webView, Sel("loadRequest:"), Send(Cls("NSURLRequest"), Sel("requestWithURL:"), nsUrl));
        }

        public void Reload(bool ignoreCache) {
            if (webView != IntPtr.Zero) {
                Send(webView, Sel(ignoreCache ? "reloadFromOrigin" : "reload"));
            }
        }

        public void GoBack() {
            if (webView != IntPtr.Zero) {
                Send(webView, Sel("goBack"));
            }
        }

        public void GoForward() {
            if (webView != IntPtr.Zero) {
                Send(webView, Sel("goForward"));
            }
        }

        public void RunScript(string script) {
            if (webView != IntPtr.Zero && !disposed) {
                // results (and errors) are reported through the bridge, the completion handler is not needed
                Send(webView, Sel("evaluateJavaScript:completionHandler:"), NSString(script), IntPtr.Zero);
            }
        }

        // ---- developer tools -----------------------------------------------------------------------------------------

        public void ShowDeveloperTools() {
            AreDeveloperToolsAllowed = true;

            // there is no public api to open the inspector, use the (private) one when available.
            // Otherwise the inspector is available through Safari's Develop menu.
            var inspector = GetInspector();
            if (inspector == IntPtr.Zero || !RespondsTo(inspector, "show")) {
                return;
            }

            Send(inspector, Sel("show"));
            DetachInspectorWhenVisible(attemptsLeft: 50);
        }

        private IntPtr GetInspector() {
            if (webView == IntPtr.Zero || !RespondsTo(webView, "_inspector")) {
                return IntPtr.Zero;
            }
            return Send(webView, Sel("_inspector"));
        }

        /// <summary>
        /// By default WebKit attaches the inspector inside the view hierarchy of the web view (it adds a sibling view and shrinks the page),
        /// which conflicts with the layout of the native control host. So, as soon as the inspector is visible, it is detached into its own window.
        /// </summary>
        private void DetachInspectorWhenVisible(int attemptsLeft) {
            DispatcherTimer.RunOnce(() => {
                var inspector = disposed ? IntPtr.Zero : GetInspector();
                if (inspector == IntPtr.Zero) {
                    return;
                }
                if (SendBool(inspector, Sel("isVisible")) != 0) {
                    if (RespondsTo(inspector, "detach")) {
                        Send(inspector, Sel("detach"));
                    }
                } else if (attemptsLeft > 0) {
                    DetachInspectorWhenVisible(attemptsLeft - 1);
                }
            }, TimeSpan.FromMilliseconds(100));
        }

        public void CloseDeveloperTools() {
            var inspector = GetInspector();
            if (inspector != IntPtr.Zero && RespondsTo(inspector, "close")) {
                Send(inspector, Sel("close"));
            }
        }

        // ---- focus and edit commands ---------------------------------------------------------------------------------

        public void Focus() {
            if (webView == IntPtr.Zero) {
                return;
            }
            var window = Send(webView, Sel("window"));
            if (window != IntPtr.Zero) {
                Send(window, Sel("makeFirstResponder:"), webView);
            }
        }

        public void Cut() => PerformAction("cut:", "cut");

        public void Copy() => PerformAction("copy:", "copy");

        public void Paste() => PerformAction("paste:", "paste");

        public void SelectAll() => PerformAction("selectAll:", "selectAll");

        private void PerformAction(string selector, string editCommand) {
            if (webView == IntPtr.Zero) {
                return;
            }
            if (RespondsTo(webView, selector)) {
                Send(webView, Sel(selector), IntPtr.Zero);
            } else {
                RunScript($"document.execCommand('{editCommand}')");
            }
        }

        // ---- callbacks from the bridge class (UI thread) -------------------------------------------------------------

        internal void OnScriptMessage(string json) {
            if (json != null) {
                owner.OnScriptMessage(json);
            }
        }

        internal bool OnNavigationRequested(string url, string method, bool isMainFrame, bool isBackForward) => owner.OnNavigating(url, method, isMainFrame, isBackForward);

        internal void OnNavigationCommitted() {
            owner.OnAddressChanged(Address);
            owner.OnNavigationCommitted(Address);
        }

        internal void OnNavigationFinished() {
            var title = Title;
            if (title != lastTitle) {
                lastTitle = title;
                owner.OnTitleChanged(title);
            }
            owner.OnNavigationFinished(Address);
        }

        internal void OnNavigationFailed(int errorCode, string description, string url) {
            const int NSURLErrorCancelled = -999;
            if (errorCode == NSURLErrorCancelled) {
                return; // aborted, for instance because a new navigation started
            }
            owner.OnNavigationFailed(url ?? Address, errorCode, description);
        }

        internal void OnContentProcessTerminated() => owner.OnProcessTerminated();

        /// <returns>The NSURLSessionAuthChallengeDisposition and the (autoreleased) NSURLCredential to use</returns>
        internal (long disposition, IntPtr credential) OnAuthenticationChallenge(IntPtr challenge) {
            const long UseCredential = 0;            // NSURLSessionAuthChallengeUseCredential
            const long PerformDefaultHandling = 1;   // NSURLSessionAuthChallengePerformDefaultHandling
            const long PersistenceForSession = 1;    // NSURLCredentialPersistenceForSession

            var protectionSpace = Send(challenge, Sel("protectionSpace"));
            var method = FromNSString(Send(protectionSpace, Sel("authenticationMethod")));
            var host = FromNSString(Send(protectionSpace, Sel("host")));

            switch (method) {
                case "NSURLAuthenticationMethodServerTrust":
                    if (owner.OnCertificateError(host)) {
                        return (UseCredential, Send(Cls("NSURLCredential"), Sel("credentialForTrust:"), Send(protectionSpace, Sel("serverTrust"))));
                    }
                    return (PerformDefaultHandling, IntPtr.Zero);

                case "NSURLAuthenticationMethodClientCertificate":
                    return (PerformDefaultHandling, IntPtr.Zero);
            }

            // basic, digest, ntlm, negotiate... for the server or for a proxy.
            // Do not insist when the provided credentials were already rejected, it would loop forever.
            if (SendLong(challenge, Sel("previousFailureCount")) > 0) {
                return (PerformDefaultHandling, IntPtr.Zero);
            }

            var isProxy = SendBool(protectionSpace, Sel("isProxy")) != 0;
            var port = (int)SendLong(protectionSpace, Sel("port"));
            var realm = FromNSString(Send(protectionSpace, Sel("realm")));
            NativeTrace.Write($"authentication requested ({method}, proxy={isProxy}) by {host}:{port}");

            if (!owner.OnAuthenticationRequested(isProxy, host, port, realm, out var userName, out var password)) {
                return (PerformDefaultHandling, IntPtr.Zero);
            }

            var credential = Send(Cls("NSURLCredential"), Sel("credentialWithUser:password:persistence:"), NSString(userName ?? string.Empty), NSString(password ?? string.Empty), PersistenceForSession);
            return (UseCredential, credential);
        }

        internal bool OnPopupRequested(string url) => owner.OnPopupRequested(url);

        internal bool OnJavascriptDialog(JavascriptDialogKind kind, string message, string defaultValue, Action<bool, string> complete) =>
            owner.OnJavascriptDialog(kind, message, defaultValue, complete);

        internal void OnSchemeTaskStarted(IntPtr task) {
            var request = Send(task, Sel("request"));
            var url = FromNSUrl(Send(request, Sel("URL")));
            var method = FromNSString(Send(request, Sel("HTTPMethod"))) ?? "GET";
            var headers = FromNSDictionary(Send(request, Sel("allHTTPHeaderFields")));

            var resourceRequest = new MacResourceRequest(this, Retain(task), url, method, headers);
            activeTasks[task] = resourceRequest;

            NativeTrace.Write($"resource requested: {method} {url}");

            if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)) {
                // cors preflight, there is nothing to ask the owner
                resourceRequest.Complete(new ResourceResponse { StatusCode = 204 });
                return;
            }

            owner.OnResourceRequested(resourceRequest);
        }

        internal void OnSchemeTaskStopped(IntPtr task) {
            if (activeTasks.TryGetValue(task, out var request)) {
                NativeTrace.Write($"resource request cancelled by the page: {request.Url}");
                activeTasks.Remove(task);
                request.Stopped = true;
                Release(request.Task);
            }
        }

        // ---- scheme task responses (UI thread) ----------------------------------------------------------------------

        internal void SendSchemeTaskResponse(MacResourceRequest request, ResourceResponse response, byte[] content) {
            if (request.Stopped || !activeTasks.Remove(request.Task)) {
                return; // cancelled by WebKit (or the browser was disposed), nothing can be sent anymore
            }

            var task = request.Task;
            try {
                var headers = new NameValueCollection();
                // pages from a scheme are cross origin to the other schemes (module scripts, fetch, ...)
                headers["Access-Control-Allow-Origin"] = "*";
                headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                headers["Access-Control-Allow-Headers"] = "*";

                var status = response?.StatusCode ?? 404;
                if (response?.Headers != null) {
                    foreach (var key in response.Headers.AllKeys) {
                        headers[key] = response.Headers[key];
                    }
                }
                if (status != 204 && status != 404) {
                    headers["Content-Type"] = GetContentType(response?.MimeType);
                }
                headers["Content-Length"] = content.Length.ToString();

                NativeTrace.Write($"resource response: {status} {headers["Content-Type"]} {content.Length} bytes {request.Url}");

                var url = Send(Send(task, Sel("request")), Sel("URL"));
                var nsResponse = Send(Send(Cls("NSHTTPURLResponse"), Sel("alloc")), Sel("initWithURL:statusCode:HTTPVersion:headerFields:"),
                                      url, (long)status, NSString("HTTP/1.1"), ToNSDictionary(headers));
                try {
                    Send(task, Sel("didReceiveResponse:"), nsResponse);
                    if (content.Length > 0) {
                        Send(task, Sel("didReceiveData:"), NSData(content));
                    }
                    Send(task, Sel("didFinish"));
                } finally {
                    Release(nsResponse);
                }
            } finally {
                Release(task);
            }
        }

        private static string GetContentType(string mimeType) {
            if (string.IsNullOrEmpty(mimeType)) {
                return "application/octet-stream";
            }
            var isText = mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
            return isText && !mimeType.Contains("charset", StringComparison.OrdinalIgnoreCase) ? mimeType + "; charset=utf-8" : mimeType;
        }

        // ---- helpers -------------------------------------------------------------------------------------------------

        internal void RunOnUIThread(Action action) {
            if (disposed) {
                return;
            }
            if (Dispatcher.UIThread.CheckAccess()) {
                action();
            } else {
                Dispatcher.UIThread.Post(() => {
                    if (!disposed) {
                        Guard(action);
                    }
                });
            }
        }

        /// <summary>
        /// Exceptions cannot cross the objective-c boundary, forward them to the owner instead.
        /// </summary>
        internal void Guard(Action action) {
            try {
                action();
            } catch (Exception e) {
                ReportError(e);
            }
        }

        internal void ReportError(Exception e) {
            try {
                owner.OnError(e);
            } catch {
                // nothing else to do
            }
        }

        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;

            if (webView != IntPtr.Zero) {
                Send(webView, Sel("stopLoading"));
                Send(webView, Sel("setNavigationDelegate:"), IntPtr.Zero);
                Send(webView, Sel("setUIDelegate:"), IntPtr.Zero);
                Send(userContentController, Sel("removeScriptMessageHandlerForName:"), NSString(BridgeHandlerName));
                Send(userContentController, Sel("removeAllUserScripts"));
                Send(webView, Sel("removeFromSuperview"));
                if (hostView != IntPtr.Zero) {
                    Send(hostView, Sel("removeFromSuperview"));
                    Release(hostView);
                    hostView = IntPtr.Zero;
                }
                Release(userContentController);
                Release(webView);
                webView = IntPtr.Zero;
                userContentController = IntPtr.Zero;
            }

            foreach (var request in activeTasks.Values) {
                request.Stopped = true;
                Release(request.Task);
            }
            activeTasks.Clear();

            if (bridge != IntPtr.Zero) {
                MacBridgeClass.DestroyInstance(bridge);
                bridge = IntPtr.Zero;
            }
        }
    }
}
