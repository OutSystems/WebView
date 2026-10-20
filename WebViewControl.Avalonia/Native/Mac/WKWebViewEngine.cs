using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WebViewControl {

    /// <summary>
    /// Renders the web view with WKWebView, the system WebKit of macOS.
    /// - the custom schemes (embedded, custom, local) are served by a WKURLSchemeHandler
    /// - the javascript bridge talks through a WKScriptMessageHandler (page to .NET) and evaluateJavaScript (.NET to page)
    /// - title, address and history state are observed with KVO
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal sealed class WKWebViewEngine : NativeWebViewEngine {

        private const string MessageHandlerName = "webviewBridge";
        private const nint NavigationActionPolicyCancel = 0;
        private const nint NavigationActionPolicyAllow = 1;
        private const nint NavigationTypeBackForward = 2;
        private const nint UserScriptInjectionTimeAtDocumentStart = 0;
        private const nint AudiovisualMediaTypeNone = 0;
        private const nuint KeyValueObservingOptionNew = 1;
        private const int UrlErrorBadUrl = -1000;
        private const int UrlErrorCancelled = -999;
        private const int WebKitErrorFrameLoadInterruptedByPolicyChange = 102;

        private static readonly string[] Schemes = { ResourceUrl.EmbeddedScheme, ResourceUrl.CustomScheme, ResourceUrl.LocalScheme };
        private static readonly string[] ObservedKeyPaths = { "title", "URL", "canGoBack", "canGoForward" };

        // engines by the instance of their delegate, which receives the callbacks of WebKit
        private static readonly ConcurrentDictionary<IntPtr, WKWebViewEngine> Engines = new();
        private static readonly Lazy<IntPtr> DelegateClass = new(RegisterDelegateClass);

        // like with CEF, the web views of the process share cookies and storage, which are discarded on exit unless persisted
        private static readonly Lazy<IntPtr> NonPersistentDataStore = new(() => ObjC.Retain(ObjC.Send(ObjC.Class("WKWebsiteDataStore"), "nonPersistentDataStore")));

        private readonly HashSet<IntPtr> activeSchemeTasks = new();
        private IntPtr webView;
        private IntPtr webViewDelegate;
        private IntPtr userContentController;
        private string mainFrameNavigationUrl;
        private bool isObservingWebView;
        private volatile bool canGoBack;
        private volatile bool canGoForward;

        public WKWebViewEngine(WebView owner) : base(owner) { }

        internal override string NativeViewHandleDescriptor => "NSView";

        public override bool CanGoBack => canGoBack;

        public override bool CanGoForward => canGoForward;

        protected override IntPtr CreateNativeView(int width, int height) {
            if (webView != IntPtr.Zero) {
                return webView;
            }

            using (ObjC.CreateAutoreleasePool()) {
                ObjC.LoadWebKit();

                webViewDelegate = ObjC.New(DelegateClass.Value);
                Engines[webViewDelegate] = this;

                var configuration = ObjC.New("WKWebViewConfiguration");
                ConfigureContent(configuration);
                ConfigureSchemes(configuration);

                var settings = WebView.Settings;
                if (!settings.PersistCache) {
                    ObjC.Send(configuration, "setWebsiteDataStore:", NonPersistentDataStore.Value);
                }
                if (settings.EnableVideoAutoplay) {
                    ObjC.SendInteger(configuration, "setMediaTypesRequiringUserActionForPlayback:", AudiovisualMediaTypeNone);
                }
                if (Owner.AllowDeveloperTools) {
                    EnableDeveloperExtras(ObjC.Send(configuration, "preferences"));
                }

                webView = ObjC.CreateWebView(new CGRect(0, 0, width, height), configuration);
                ObjC.Release(configuration);

                // no autoresizing: the holder view of Avalonia starts empty and grows, which would grow the web view beyond it
                // (views don't clip their subviews), the frame follows the size of the host control instead (see SetSize)
                ObjC.Send(webView, "setNavigationDelegate:", webViewDelegate);
                ObjC.Send(webView, "setUIDelegate:", webViewDelegate);
                foreach (var keyPath in ObservedKeyPaths) {
                    ObjC.AddObserver(webView, webViewDelegate, keyPath, KeyValueObservingOptionNew);
                }
                isObservingWebView = true;

                if (!string.IsNullOrEmpty(settings.UserAgent)) {
                    var userAgent = ObjC.CreateString(settings.UserAgent);
                    ObjC.Send(webView, "setCustomUserAgent:", userAgent);
                    ObjC.Release(userAgent);
                }

                if (Owner.AllowDeveloperTools) {
                    SetInspectable();
                }
            }

            OnBrowserCreated();

            return webView;
        }

        private void ConfigureContent(IntPtr configuration) {
            userContentController = ObjC.Retain(ObjC.Send(configuration, "userContentController"));

            // the bridge is installed in the main frame before any script of the page runs
            var source = ObjC.CreateString(BridgeScript);
            var userScript = ObjC.CreateUserScript(source, UserScriptInjectionTimeAtDocumentStart, forMainFrameOnly: true);
            ObjC.Send(userContentController, "addUserScript:", userScript);
            ObjC.Release(userScript);
            ObjC.Release(source);

            var handlerName = ObjC.CreateString(MessageHandlerName);
            ObjC.Send(userContentController, "addScriptMessageHandler:name:", webViewDelegate, handlerName);
            ObjC.Release(handlerName);
        }

        private void ConfigureSchemes(IntPtr configuration) {
            var webViewClass = ObjC.Class("WKWebView");
            foreach (var scheme in Schemes) {
                var nsScheme = ObjC.CreateString(scheme);
                // WebKit throws when registering a handler for a scheme it handles itself
                if (!ObjC.GetBool(webViewClass, "handlesURLScheme:", nsScheme)) {
                    ObjC.Send(configuration, "setURLSchemeHandler:forURLScheme:", webViewDelegate, nsScheme);
                }
                ObjC.Release(nsScheme);
            }
        }

        private void SetInspectable() {
            // inspectable by Safari's Web Inspector (macOS 13.3+)
            if (ObjC.RespondsTo(webView, "setInspectable:")) {
                ObjC.SendBool(webView, "setInspectable:", true);
            }
        }

        private static void EnableDeveloperExtras(IntPtr preferences) {
            // enables the inspector of the web view (and its "Inspect Element" context menu): private api, used when available
            if (ObjC.RespondsTo(preferences, "_setDeveloperExtrasEnabled:")) {
                ObjC.SendBool(preferences, "_setDeveloperExtrasEnabled:", true);
            }
        }

        protected override void DestroyNativeBrowser() {
            if (webView != IntPtr.Zero) {
                using (ObjC.CreateAutoreleasePool()) {
                    // the scheme tasks are stopped by WebKit once the web view goes away
                    foreach (var task in activeSchemeTasks) {
                        ObjC.Release(task);
                    }
                    activeSchemeTasks.Clear();

                    if (isObservingWebView) {
                        foreach (var keyPath in ObservedKeyPaths) {
                            ObjC.RemoveObserver(webView, webViewDelegate, keyPath);
                        }
                        isObservingWebView = false;
                    }
                    ObjC.Send(webView, "setNavigationDelegate:", IntPtr.Zero);
                    ObjC.Send(webView, "setUIDelegate:", IntPtr.Zero);
                    ObjC.Send(webView, "stopLoading");

                    // the user content controller retains the message handler: remove it to break the cycle
                    var handlerName = ObjC.CreateString(MessageHandlerName);
                    ObjC.Send(userContentController, "removeScriptMessageHandlerForName:", handlerName);
                    ObjC.Release(handlerName);
                    ObjC.Send(userContentController, "removeAllUserScripts");
                    ObjC.Release(userContentController);
                    userContentController = IntPtr.Zero;

                    ObjC.Send(webView, "removeFromSuperview");
                    ObjC.Release(webView);
                    webView = IntPtr.Zero;
                }
            }

            if (webViewDelegate != IntPtr.Zero) {
                Engines.TryRemove(webViewDelegate, out _);
                ObjC.Release(webViewDelegate);
                webViewDelegate = IntPtr.Zero;
            }
        }

        protected override void NavigateTo(string url) {
            using (ObjC.CreateAutoreleasePool()) {
                var nsUrl = ObjC.CreateUrl(url);
                if (nsUrl == IntPtr.Zero) {
                    OnNavigationFailed(url, UrlErrorBadUrl, isAborted: false);
                    return;
                }
                var request = ObjC.Send(ObjC.Class("NSURLRequest"), "requestWithURL:", nsUrl);
                ObjC.Send(webView, "loadRequest:", request);
                ObjC.Release(nsUrl);
            }
        }

        protected override void GoBackCore() => ObjC.Send(webView, "goBack");

        protected override void GoForwardCore() => ObjC.Send(webView, "goForward");

        protected override void ReloadCore(bool ignoreCache) => ObjC.Send(webView, ignoreCache ? "reloadFromOrigin" : "reload");

        protected override void ShowDeveloperToolsCore() {
            SetInspectable();
            using (ObjC.CreateAutoreleasePool()) {
                EnableDeveloperExtras(ObjC.Send(ObjC.Send(webView, "configuration"), "preferences"));
            }
            // there's no public api to show the inspector: use the private one when available
            if (ObjC.RespondsTo(webView, "_inspector")) {
                var inspector = ObjC.Send(webView, "_inspector");
                if (ObjC.RespondsTo(inspector, "show")) {
                    ObjC.Send(inspector, "show");
                }
            }
        }

        protected override void CloseDeveloperToolsCore() {
            if (ObjC.RespondsTo(webView, "_inspector")) {
                var inspector = ObjC.Send(webView, "_inspector");
                if (ObjC.RespondsTo(inspector, "close")) {
                    ObjC.Send(inspector, "close");
                }
            }
        }

        protected override void ExecuteEditCommandCore(EditCommand command) {
            var action = command switch {
                EditCommand.Cut => "cut:",
                EditCommand.Copy => "copy:",
                EditCommand.Paste => "paste:",
                EditCommand.SelectAll => "selectAll:",
                EditCommand.Undo => "undo:",
                EditCommand.Redo => "redo:",
                EditCommand.Delete => "delete:",
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown edit command")
            };

            if (ObjC.RespondsTo(webView, action)) {
                ObjC.Send(webView, action, IntPtr.Zero);
                return;
            }

            if (command == EditCommand.Undo || command == EditCommand.Redo) {
                var undoManager = ObjC.Send(webView, "undoManager");
                var isUndo = command == EditCommand.Undo;
                if (undoManager != IntPtr.Zero && ObjC.GetBool(undoManager, isUndo ? "canUndo" : "canRedo")) {
                    ObjC.Send(undoManager, isUndo ? "undo" : "redo");
                }
            }
        }

        protected override void FocusCore() {
            var window = ObjC.Send(webView, "window");
            if (window != IntPtr.Zero) {
                ObjC.Send(window, "makeFirstResponder:", webView);
            }
        }

        internal override void SetSize(double width, double height) {
            if (webView != IntPtr.Zero && width > 0 && height > 0) {
                ObjC.SendRect(webView, "setFrame:", new CGRect(0, 0, width, height));
            }
        }

        protected override void SetZoom(double percentage) {
            if (ObjC.RespondsTo(webView, "setPageZoom:")) {
                ObjC.SendDouble(webView, "setPageZoom:", percentage);
            }
        }

        protected override void PostMessageToPage(string json) {
            if (webView == IntPtr.Zero) {
                return;
            }
            using (ObjC.CreateAutoreleasePool()) {
                var script = ObjC.CreateString("window.webviewBridge && window.webviewBridge.__receive(" + json + ")");
                ObjC.Send(webView, "evaluateJavaScript:completionHandler:", script, IntPtr.Zero);
                ObjC.Release(script);
            }
        }

        private void OnScriptMessage(IntPtr message) {
            // only the main frame talks to .NET (the bridge is not installed in iframes, but they could post messages too)
            var frameInfo = ObjC.Send(message, "frameInfo");
            if (frameInfo == IntPtr.Zero || !ObjC.GetBool(frameInfo, "isMainFrame")) {
                return;
            }
            OnMessageReceived(ObjC.GetString(ObjC.Send(message, "body")));
        }

        private bool ShouldCancelNavigation(IntPtr navigationAction) {
            var targetFrame = ObjC.Send(navigationAction, "targetFrame");
            if (targetFrame == IntPtr.Zero) {
                // the navigation opens a new window: handled by the popup callback
                return false;
            }

            var request = ObjC.Send(navigationAction, "request");
            var url = ObjC.GetUrlString(ObjC.Send(request, "URL"));
            var method = ObjC.GetString(ObjC.Send(request, "HTTPMethod"));
            var isMainFrame = ObjC.GetBool(targetFrame, "isMainFrame");
            var isBackOrForward = ObjC.GetInteger(navigationAction, "navigationType") == NavigationTypeBackForward;

            var cancel = OnBeforeNavigate(url, method, isMainFrame, isBackOrForward);
            if (!cancel && isMainFrame) {
                mainFrameNavigationUrl = url;
            }
            return cancel;
        }

        private void OnNavigationError(IntPtr error) {
            var code = (int)ObjC.GetInteger(error, "code");
            var domain = ObjC.GetString(ObjC.Send(error, "domain"));
            var userInfo = ObjC.Send(error, "userInfo");
            string failedUrl = null;
            if (userInfo != IntPtr.Zero) {
                var key = ObjC.CreateString("NSErrorFailingURLStringKey");
                failedUrl = ObjC.GetString(ObjC.Send(userInfo, "objectForKey:", key));
                ObjC.Release(key);
            }
            var isAborted = (domain == "NSURLErrorDomain" && code == UrlErrorCancelled) ||
                            (domain == "WebKitErrorDomain" && code == WebKitErrorFrameLoadInterruptedByPolicyChange);
            OnNavigationFailed(failedUrl ?? mainFrameNavigationUrl, code, isAborted);
        }

        private void OnObservedValueChanged(string keyPath) {
            switch (keyPath) {
                case "title":
                    OnTitleChanged(ObjC.GetString(ObjC.Send(webView, "title")));
                    break;
                case "URL":
                    OnAddressChanged(ObjC.GetUrlString(ObjC.Send(webView, "URL")));
                    break;
                case "canGoBack":
                    canGoBack = ObjC.GetBool(webView, "canGoBack");
                    break;
                case "canGoForward":
                    canGoForward = ObjC.GetBool(webView, "canGoForward");
                    break;
            }
        }

        private void OnCreateWebView(IntPtr navigationAction) {
            var targetUrl = ObjC.GetUrlString(ObjC.Send(ObjC.Send(navigationAction, "request"), "URL"));
            var sourceFrame = ObjC.Send(navigationAction, "sourceFrame");
            var sourceUrl = sourceFrame == IntPtr.Zero ? null : ObjC.GetUrlString(ObjC.Send(ObjC.Send(sourceFrame, "request"), "URL"));
            // popups are never opened by the web view (when not handled, they are opened in the external browser)
            OnBeforePopup(targetUrl, sourceUrl);
        }

        private void OnStartSchemeTask(IntPtr task) {
            ObjC.Retain(task);
            activeSchemeTasks.Add(task);

            var request = ObjC.Send(task, "request");
            var url = ObjC.GetUrlString(ObjC.Send(request, "URL"));
            var method = ObjC.GetString(ObjC.Send(request, "HTTPMethod"));
            var headers = ObjC.GetStringDictionary(ObjC.Send(request, "allHTTPHeaderFields"));
            var requestDetails = new NativeRequestDetails(url, method, GetResourceType(url, headers["Accept"]), headers);

            RespondToSchemeTask(task, requestDetails);
        }

        private async void RespondToSchemeTask(IntPtr task, NativeRequestDetails request) {
            var result = await GetResourceAsync(request).ConfigureAwait(false);
            RunOnUIThread(() => CompleteSchemeTask(task, result));
        }

        private void CompleteSchemeTask(IntPtr task, NativeResourceResult result) {
            if (!activeSchemeTasks.Remove(task)) {
                // stopped meanwhile: calling the task now would throw
                return;
            }

            try {
                using (ObjC.CreateAutoreleasePool()) {
                    var url = ObjC.Send(ObjC.Send(task, "request"), "URL");
                    var httpVersion = ObjC.CreateString("HTTP/1.1");
                    var headerFields = ObjC.CreateDictionary(result.GetHeaders());
                    var response = ObjC.CreateHttpResponse(url, result.StatusCode, httpVersion, headerFields);
                    ObjC.Send(task, "didReceiveResponse:", response);
                    if (result.Content.Length > 0) {
                        var data = ObjC.CreateData(result.Content);
                        ObjC.Send(task, "didReceiveData:", data);
                        ObjC.Release(data);
                    }
                    ObjC.Send(task, "didFinish");
                    ObjC.Release(response);
                    ObjC.Release(headerFields);
                    ObjC.Release(httpVersion);
                }
            } finally {
                ObjC.Release(task);
            }
        }

        private void OnStopSchemeTask(IntPtr task) {
            if (activeSchemeTasks.Remove(task)) {
                ObjC.Release(task);
            }
        }

        private ResourceType GetResourceType(string url, string accept) {
            // WebKit doesn't tell the type of the resource requested: infer it from the navigation and the request
            if (url == mainFrameNavigationUrl) {
                return ResourceType.MainFrame;
            }
            if (accept != null) {
                if (accept.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)) {
                    return ResourceType.SubFrame;
                }
                if (accept.StartsWith("text/css", StringComparison.OrdinalIgnoreCase)) {
                    return ResourceType.Stylesheet;
                }
                if (accept.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
                    return ResourceType.Image;
                }
            }
            var extension = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() : string.Empty;
            switch (extension) {
                case ".js":
                case ".mjs":
                    return ResourceType.Script;
                case ".woff":
                case ".woff2":
                case ".ttf":
                case ".otf":
                case ".eot":
                    return ResourceType.FontResource;
                default:
                    return ResourceType.SubResource;
            }
        }

        private static void Dispatch(IntPtr self, Action<WKWebViewEngine> action) {
            try {
                if (Engines.TryGetValue(self, out var engine) && !engine.IsDisposed) {
                    action(engine);
                }
            } catch (Exception e) {
                // exceptions must not cross back into native code
                Trace.TraceError("WebView: {0}", e);
            }
        }

        private static unsafe IntPtr RegisterDelegateClass() {
            var protocols = new[] { "WKNavigationDelegate", "WKUIDelegate", "WKScriptMessageHandler", "WKURLSchemeHandler" };
            return ObjC.RegisterClass(
                "WebViewControlWKWebViewDelegate_" + Guid.NewGuid().ToString("N"),
                "NSObject",
                protocols,
                ("userContentController:didReceiveScriptMessage:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidReceiveScriptMessage, "v@:@@"),
                ("webView:startURLSchemeTask:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&StartUrlSchemeTask, "v@:@@"),
                ("webView:stopURLSchemeTask:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&StopUrlSchemeTask, "v@:@@"),
                ("webView:decidePolicyForNavigationAction:decisionHandler:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DecidePolicyForNavigationAction, "v@:@@@?"),
                ("webView:didStartProvisionalNavigation:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidStartProvisionalNavigation, "v@:@@"),
                ("webView:didCommitNavigation:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidCommitNavigation, "v@:@@"),
                ("webView:didFinishNavigation:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFinishNavigation, "v@:@@"),
                ("webView:didFailProvisionalNavigation:withError:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFailNavigation, "v@:@@@"),
                ("webView:didFailNavigation:withError:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFailNavigation, "v@:@@@"),
                ("webViewWebContentProcessDidTerminate:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&WebContentProcessDidTerminate, "v@:@"),
                ("webView:createWebViewWithConfiguration:forNavigationAction:windowFeatures:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)&CreateWebView, "@@:@@@@"),
                ("observeValueForKeyPath:ofObject:change:context:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&ObserveValueForKeyPath, "v@:@@@^v"));
        }

        [UnmanagedCallersOnly]
        private static void DidReceiveScriptMessage(IntPtr self, IntPtr selector, IntPtr userContentController, IntPtr message) {
            Dispatch(self, engine => engine.OnScriptMessage(message));
        }

        [UnmanagedCallersOnly]
        private static void StartUrlSchemeTask(IntPtr self, IntPtr selector, IntPtr webView, IntPtr task) {
            Dispatch(self, engine => engine.OnStartSchemeTask(task));
        }

        [UnmanagedCallersOnly]
        private static void StopUrlSchemeTask(IntPtr self, IntPtr selector, IntPtr webView, IntPtr task) {
            Dispatch(self, engine => engine.OnStopSchemeTask(task));
        }

        [UnmanagedCallersOnly]
        private static void DecidePolicyForNavigationAction(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigationAction, IntPtr decisionHandler) {
            var policy = NavigationActionPolicyAllow;
            Dispatch(self, engine => {
                if (engine.ShouldCancelNavigation(navigationAction)) {
                    policy = NavigationActionPolicyCancel;
                }
            });
            // the decision handler must always be called
            ObjC.InvokeBlock(decisionHandler, policy);
        }

        [UnmanagedCallersOnly]
        private static void DidStartProvisionalNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation) {
            Dispatch(self, engine => engine.OnNavigationStarted());
        }

        [UnmanagedCallersOnly]
        private static void DidCommitNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation) {
            Dispatch(self, engine => engine.OnNavigationCommitted());
        }

        [UnmanagedCallersOnly]
        private static void DidFinishNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation) {
            Dispatch(self, engine => engine.OnNavigationCompleted(ObjC.GetUrlString(ObjC.Send(engine.webView, "URL"))));
        }

        [UnmanagedCallersOnly]
        private static void DidFailNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation, IntPtr error) {
            Dispatch(self, engine => engine.OnNavigationError(error));
        }

        [UnmanagedCallersOnly]
        private static void WebContentProcessDidTerminate(IntPtr self, IntPtr selector, IntPtr webView) {
            Dispatch(self, engine => engine.OnRenderProcessTerminated(RenderProcessTerminationStatus.Crashed));
        }

        [UnmanagedCallersOnly]
        private static IntPtr CreateWebView(IntPtr self, IntPtr selector, IntPtr webView, IntPtr configuration, IntPtr navigationAction, IntPtr windowFeatures) {
            Dispatch(self, engine => engine.OnCreateWebView(navigationAction));
            return IntPtr.Zero;
        }

        [UnmanagedCallersOnly]
        private static void ObserveValueForKeyPath(IntPtr self, IntPtr selector, IntPtr keyPath, IntPtr target, IntPtr change, IntPtr context) {
            Dispatch(self, engine => engine.OnObservedValueChanged(ObjC.GetString(keyPath)));
        }
    }
}
