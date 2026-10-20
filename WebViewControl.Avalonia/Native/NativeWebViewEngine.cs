using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace WebViewControl {

    /// <summary>
    /// Base of the engines that render with the web engine of the operating system: WKWebView on macOS and WebView2 on Windows.
    /// The page talks to .NET through a small javascript bridge (WebViewBridge.js), injected at document start in the main frame,
    /// that replaces the CefGlue javascript bindings: registered objects, script evaluation and uncaught errors.
    /// The native engines must be used from the UI thread, which is where every call into the platform happens.
    /// </summary>
    internal abstract class NativeWebViewEngine : IWebViewEngine {

        private const string MainFrameName = "";
        private const string BridgeScriptResourceName = "WebViewControl.Native.WebViewBridge.js";

        // V8 (WebView2) frames: "    at method (location:line:column)" or "    at location:line:column"
        private static readonly Regex V8StackFrameRegex = new(@"^\s*at\s+(?:(?<method>.*?)\s+\()?(?<location>[^\s()]+):(?<line>\d+):(?<column>\d+)\)?\s*$", RegexOptions.Compiled);

        // JavaScriptCore (WKWebView) frames: "method@location:line:column", or "method@" for evaluated code
        private static readonly Regex WebKitStackFrameRegex = new(@"^\s*(?<method>[^@]*)@(?:(?<location>.+):(?<line>\d+):(?<column>\d+)|(?<location>[^:]*))\s*$", RegexOptions.Compiled);

        private static readonly Lazy<string> bridgeScript = new(LoadBridgeScript);

        private readonly object syncRoot = new();
        private readonly ConcurrentDictionary<string, NativeJavascriptObject> javascriptObjects = new();
        private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> pendingEvaluations = new();
        private readonly NativeWebViewHost host;
        private long lastEvaluationId;
        private int javascriptContextId;
        private string pendingUrl;
        private double zoomPercentage = 1;
        private volatile string address;
        private string reportedAddress;
        private volatile string title;
        private volatile bool isBrowserInitialized;
        private volatile bool isJavascriptContextActive;
        private volatile bool isLoading;
        private volatile bool isDisposed;

        protected NativeWebViewEngine(WebView owner) {
            Owner = owner;
            MainFrame = new NativeJavascriptFrame(this);
            host = new NativeWebViewHost(this);
        }

        protected WebView Owner { get; }

        protected bool IsDisposed => isDisposed;

        protected static string BridgeScript => bridgeScript.Value;

        private IJavascriptFrame MainFrame { get; }

        /// <summary>
        /// The descriptor of the native view handle, as expected by Avalonia (eg: NSView, HWND).
        /// </summary>
        internal abstract string NativeViewHandleDescriptor { get; }

        public Control View => host;

        public bool IsBrowserInitialized => isBrowserInitialized;

        public bool IsJavascriptEngineInitialized => isJavascriptContextActive;

        public bool IsLoading => isLoading;

        public string Address => address;

        public string Title => title;

        public abstract bool CanGoBack { get; }

        public abstract bool CanGoForward { get; }

        public double ZoomPercentage {
            get => zoomPercentage;
            set {
                zoomPercentage = value;
                RunOnUIThread(() => {
                    if (isBrowserInitialized && !isDisposed) {
                        SetZoom(value);
                    }
                });
            }
        }

        public void CreateBrowser(int width, int height) {
            RunOnUIThread(() => GetOrCreateNativeView(width, height));
        }

        /// <summary>
        /// Returns the native view, creating the browser when needed. Called on the UI thread.
        /// </summary>
        internal IntPtr GetOrCreateNativeView(int width, int height) {
            if (isDisposed) {
                return IntPtr.Zero;
            }
            return CreateNativeView(Math.Max(1, width), Math.Max(1, height));
        }

        public void Navigate(string url) {
            address = url;
            lock (syncRoot) {
                if (!isBrowserInitialized) {
                    pendingUrl = url;
                    return;
                }
            }
            RunOnUIThread(() => {
                if (!isDisposed) {
                    NavigateTo(url);
                }
            });
        }

        public void GoBack() => RunOnUIThread(() => ExecuteWhenReady(GoBackCore));

        public void GoForward() => RunOnUIThread(() => ExecuteWhenReady(GoForwardCore));

        public void Reload(bool ignoreCache) => RunOnUIThread(() => ExecuteWhenReady(() => ReloadCore(ignoreCache)));

        public void ShowDeveloperTools() => RunOnUIThread(() => ExecuteWhenReady(ShowDeveloperToolsCore));

        public void CloseDeveloperTools() => RunOnUIThread(() => ExecuteWhenReady(CloseDeveloperToolsCore));

        public void SetAccessibilityState(bool enabled) {
            // the system engines expose their accessibility tree to the platform on their own
        }

        public void ExecuteEditCommand(EditCommand command) => RunOnUIThread(() => ExecuteWhenReady(() => ExecuteEditCommandCore(command)));

        public void Focus() => RunOnUIThread(() => ExecuteWhenReady(FocusCore));

        public IJavascriptFrame GetFrame(string frameName) {
            // scripts can only be evaluated in the main frame
            return string.IsNullOrEmpty(frameName) ? MainFrame : null;
        }

        public string[] GetFrameNames() => new[] { MainFrameName };

        public bool IsJavascriptObjectRegistered(string name) => javascriptObjects.ContainsKey(name);

        public void RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall) {
            var javascriptObject = new NativeJavascriptObject(name, objectToBind, interceptCall);
            if (!javascriptObjects.TryAdd(name, javascriptObject)) {
                return;
            }
            RunOnUIThread(() => {
                if (isJavascriptContextActive) {
                    PostToPage(CreateDefineObjectsMessage(new[] { javascriptObject }));
                }
            });
        }

        public void UnregisterJavascriptObject(string name) {
            javascriptObjects.TryRemove(name, out _);
            // even when not registered: the page may be waiting for the object, which won't come
            RunOnUIThread(() => {
                if (isJavascriptContextActive) {
                    PostToPage(new { type = "deleteObject", name });
                }
            });
        }

        public void Dispose() {
            if (isDisposed) {
                return;
            }
            isDisposed = true;
            isJavascriptContextActive = false;
            javascriptObjects.Clear();
            CancelPendingEvaluations();
            RunOnUIThread(DestroyNativeBrowser);
        }

        /// <summary>
        /// Creates (once) the native browser and returns its view.
        /// When the browser is ready to navigate, <see cref="OnBrowserCreated"/> must be called.
        /// </summary>
        protected abstract IntPtr CreateNativeView(int width, int height);

        protected abstract void DestroyNativeBrowser();

        protected abstract void NavigateTo(string url);

        protected abstract void GoBackCore();

        protected abstract void GoForwardCore();

        protected abstract void ReloadCore(bool ignoreCache);

        protected abstract void ShowDeveloperToolsCore();

        protected abstract void CloseDeveloperToolsCore();

        protected abstract void ExecuteEditCommandCore(EditCommand command);

        protected abstract void FocusCore();

        protected abstract void SetZoom(double percentage);

        /// <summary>
        /// The host control was resized (in device independent units). Called on the UI thread.
        /// </summary>
        internal virtual void SetSize(double width, double height) { }

        /// <summary>
        /// Sends a message (JSON) to the bridge of the current page.
        /// </summary>
        protected abstract void PostMessageToPage(string json);

        protected void OnBrowserCreated() {
            string url;
            lock (syncRoot) {
                isBrowserInitialized = true;
                url = pendingUrl;
                pendingUrl = null;
            }

            if (zoomPercentage != 1) {
                SetZoom(zoomPercentage);
            }

            Owner.HandleBrowserInitialized();

            if (url != null) {
                NavigateTo(url);
            }
        }

        protected void OnBrowserCreationFailed(Exception exception) {
            Trace.TraceError("WebView: failed to create the browser: {0}", exception);
            Owner.HandleUnhandledException(exception);
        }

        protected void OnAddressChanged(string newAddress) {
            if (string.IsNullOrEmpty(newAddress) || newAddress == reportedAddress) {
                return;
            }
            reportedAddress = newAddress;
            // update the address before notifying, the web view only navigates when the address differs from the engine one
            address = newAddress;
            Owner.HandleAddressChanged(newAddress);
        }

        protected void OnTitleChanged(string newTitle) {
            title = newTitle;
            Owner.HandleTitleChanged();
        }

        protected void OnNavigationStarted() => isLoading = true;

        /// <summary>
        /// The main frame committed a new document: the javascript context of the previous one is gone.
        /// </summary>
        protected void OnNavigationCommitted() => ReleaseJavascriptContext();

        protected void OnNavigationCompleted(string url) {
            isLoading = false;
            OnAddressChanged(url);
            Owner.HandleLoadEnd(MainFrame);
        }

        protected void OnNavigationFailed(string url, int errorCode, bool isAborted) {
            isLoading = false;
            Owner.HandleLoadError(url ?? address, errorCode, isAborted, MainFrameName, true);
        }

        protected void OnRenderProcessTerminated(RenderProcessTerminationStatus status) {
            isLoading = false;
            ReleaseJavascriptContext();
            Owner.HandleRenderProcessTerminated(status);
        }

        /// <returns>True when the navigation must be cancelled.</returns>
        protected bool OnBeforeNavigate(string url, string method, bool isMainFrame, bool isBackOrForward) {
            var request = new NativeRequestDetails(url, method, isMainFrame ? ResourceType.MainFrame : ResourceType.SubFrame);
            return Owner.HandleBeforeNavigate(request, isBackOrForward);
        }

        /// <returns>True when the popup was handled and the engine must not open it.</returns>
        protected bool OnBeforePopup(string targetUrl, string sourceUrl) {
            return Owner.HandleBeforePopup(targetUrl, string.IsNullOrEmpty(sourceUrl) ? address : sourceUrl);
        }

        /// <summary>
        /// Gets the response for a request of the custom schemes (the html loaded with LoadHtml, embedded resources and the
        /// resources served by the BeforeResourceLoad handlers). The handlers run on a background thread, like they do with CEF.
        /// </summary>
        protected async Task<NativeResourceResult> GetResourceAsync(NativeRequestDetails request) {
            try {
                if (Owner.TryGetHtmlToLoad(request.Url, out var html)) {
                    return NativeResourceResult.Text(html, "text/html");
                }

                var resourceHandler = await Task.Run(() => Owner.HandleResourceRequest(request, () => new NativeResourceResponse())).ConfigureAwait(false);
                if (resourceHandler?.Handler is not NativeResourceResponse response) {
                    return NativeResourceResult.NotFound;
                }

                await response.Completion.ConfigureAwait(false);
                return response.ToResult(request.Url);
            } catch (Exception e) {
                Owner.HandleUnhandledException(e);
                return NativeResourceResult.NotFound;
            }
        }

        /// <summary>
        /// Handles a message (JSON) sent by the bridge of the page in the main frame.
        /// </summary>
        protected void OnMessageReceived(string json) {
            if (isDisposed || string.IsNullOrEmpty(json)) {
                return;
            }

            JsonDocument document;
            try {
                document = JsonDocument.Parse(json);
            } catch (JsonException e) {
                Trace.TraceWarning("WebView: ignored invalid bridge message: {0}", e.Message);
                return;
            }

            using (document) {
                var message = document.RootElement;
                switch (GetString(message, "type")) {
                    case "ready":
                        OnJavascriptContextCreated();
                        break;
                    case "call":
                        OnJavascriptCall(GetLong(message, "id"), GetString(message, "objectName"), GetString(message, "methodName"), GetString(message, "args"));
                        break;
                    case "bindObject":
                        OnBindObject(GetString(message, "name"));
                        break;
                    case "evaluationResult":
                        OnEvaluationResult(GetLong(message, "id"), message.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True, GetString(message, "result"), GetString(message, "error"));
                        break;
                    case "uncaughtException":
                        Owner.HandleJavascriptUncaughtException(GetString(message, "message"), ParseStackFrames(GetString(message, "stack")), MainFrameName);
                        break;
                }
            }
        }

        /// <summary>
        /// Runs the action on the UI thread (right away when already there). Errors are forwarded to the web view.
        /// </summary>
        protected void RunOnUIThread(Action action) {
            void SafeAction() {
                try {
                    action();
                } catch (Exception e) {
                    Trace.TraceError("WebView: {0}", e);
                    Owner.HandleUnhandledException(e);
                }
            }

            if (Dispatcher.UIThread.CheckAccess()) {
                SafeAction();
            } else {
                Dispatcher.UIThread.Post(SafeAction);
            }
        }

        internal async Task<T> EvaluateScript<T>(string script, TimeSpan? timeout) {
            var id = Interlocked.Increment(ref lastEvaluationId);
            var evaluation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingEvaluations[id] = evaluation;

            RunOnUIThread(() => {
                if (isDisposed || !isJavascriptContextActive) {
                    // the document is gone (or not there yet)
                    CancelEvaluation(id);
                    return;
                }
                PostToPage(new { type = "evaluate", id, script });
            });

            using (var timeoutCancellation = new CancellationTokenSource()) {
                if (timeout.HasValue && timeout.Value > TimeSpan.Zero) {
                    timeoutCancellation.CancelAfter(timeout.Value);
                }
                using (timeoutCancellation.Token.Register(() => CancelEvaluation(id))) {
                    // canceled evaluations surface as TaskCanceledException, as with CEF
                    var resultJson = await evaluation.Task.ConfigureAwait(false);
                    return NativeJson.Deserialize<T>(resultJson);
                }
            }
        }

        private void ExecuteWhenReady(Action action) {
            if (isBrowserInitialized && !isDisposed) {
                action();
            }
        }

        private void PostToPage(object message) {
            if (isDisposed) {
                return;
            }
            PostMessageToPage(JsonSerializer.Serialize(message));
        }

        private void OnJavascriptContextCreated() {
            // a new document in the main frame: the previous context (if not released yet) is gone
            ReleaseJavascriptContext();

            javascriptContextId++;
            isJavascriptContextActive = true;

            var objects = javascriptObjects.Values.ToArray();
            if (objects.Length > 0) {
                PostToPage(CreateDefineObjectsMessage(objects));
            }

            Owner.HandleJavascriptContextCreated(MainFrame);
        }

        private void ReleaseJavascriptContext() {
            if (!isJavascriptContextActive) {
                return;
            }
            isJavascriptContextActive = false;
            // release before canceling the evaluations, so that the executors of the released context ignore the cancelations
            Owner.HandleJavascriptContextReleased(MainFrameName, address);
            CancelPendingEvaluations();
        }

        private void OnJavascriptCall(long callId, string objectName, string methodName, string args) {
            var contextId = javascriptContextId;
            if (!javascriptObjects.TryGetValue(objectName ?? "", out var javascriptObject)) {
                PostCallResult(contextId, callId, null, $"Object '{objectName}' is not registered");
                return;
            }

            Task.Run(async () => {
                string result = null;
                string error = null;
                try {
                    result = await javascriptObject.InvokeAsync(methodName, args).ConfigureAwait(false);
                } catch (Exception e) {
                    error = e.Message;
                }
                RunOnUIThread(() => PostCallResult(contextId, callId, result, error));
            });
        }

        private void PostCallResult(int contextId, long callId, string result, string error) {
            if (!isJavascriptContextActive || contextId != javascriptContextId) {
                // the call was made by a document that is gone
                return;
            }
            PostToPage(new { type = "callResult", id = callId, success = error == null, result, error });
        }

        private void OnBindObject(string name) {
            // like CefGlue, an object that is not registered yet is bound once registered (see RegisterJavascriptObject)
            if (javascriptObjects.TryGetValue(name ?? "", out var javascriptObject)) {
                PostToPage(CreateDefineObjectsMessage(new[] { javascriptObject }));
            }
        }

        private void OnEvaluationResult(long id, bool success, string result, string error) {
            if (!pendingEvaluations.TryRemove(id, out var evaluation)) {
                return;
            }
            if (success) {
                evaluation.TrySetResult(result);
            } else {
                evaluation.TrySetException(new JavascriptEvaluationException(error));
            }
        }

        private void CancelEvaluation(long id) {
            if (pendingEvaluations.TryRemove(id, out var evaluation)) {
                evaluation.TrySetCanceled();
            }
        }

        private void CancelPendingEvaluations() {
            foreach (var id in pendingEvaluations.Keys.ToArray()) {
                CancelEvaluation(id);
            }
        }

        private static object CreateDefineObjectsMessage(IEnumerable<NativeJavascriptObject> objects) {
            return new {
                type = "defineObjects",
                objects = objects.Select(o => new { name = o.Name, methods = o.MethodNames.ToArray() }).ToArray()
            };
        }

        private static string GetString(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static long GetLong(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0;
        }

        private static JavascriptStackFrame[] ParseStackFrames(string stack) {
            if (string.IsNullOrEmpty(stack)) {
                return Array.Empty<JavascriptStackFrame>();
            }
            var frames = new List<JavascriptStackFrame>();
            foreach (var line in stack.Split('\n')) {
                var match = V8StackFrameRegex.Match(line);
                if (!match.Success) {
                    match = WebKitStackFrameRegex.Match(line);
                }
                if (match.Success) {
                    frames.Add(new JavascriptStackFrame(match.Groups["method"].Value, match.Groups["location"].Value, ParseNumber(match.Groups["column"]), ParseNumber(match.Groups["line"])));
                }
            }
            return frames.ToArray();
        }

        private static int ParseNumber(Group group) => int.TryParse(group.Value, out var number) ? number : 0;

        private static string LoadBridgeScript() {
            using (var stream = typeof(NativeWebViewEngine).Assembly.GetManifestResourceStream(BridgeScriptResourceName)) {
                if (stream == null) {
                    throw new InvalidOperationException($"Resource not found: {BridgeScriptResourceName}");
                }
                using (var reader = new StreamReader(stream)) {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// An error thrown by an evaluated script. The message is the thrown value, which the javascript executor parses.
        /// </summary>
        private sealed class JavascriptEvaluationException : Exception {
            public JavascriptEvaluationException(string message) : base(message) { }
        }

        private sealed class NativeJavascriptFrame : IJavascriptFrame {

            private readonly NativeWebViewEngine engine;

            public NativeJavascriptFrame(NativeWebViewEngine engine) {
                this.engine = engine;
            }

            public string Name => MainFrameName;

            public string Url => engine.Address;

            public bool IsMain => true;

            public bool IsValid => !engine.IsDisposed;

            public Task<T> EvaluateScript<T>(string script, TimeSpan? timeout) => engine.EvaluateScript<T>(script, timeout);

            public void LoadUrl(string url) => engine.Navigate(url);
        }
    }
}
