using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace WebViewControl {

    public delegate void BeforeNavigateEventHandler(Request request);
    public delegate void BeforeResourceLoadEventHandler(ResourceHandler resourceHandler);
    public delegate void NavigatedEventHandler(string url, string frameName);
    public delegate void LoadFailedEventHandler(string url, int errorCode, string frameName);
    public delegate void ResourceLoadFailedEventHandler(string resourceUrl);
    public delegate void DownloadProgressChangedEventHandler(string resourcePath, long receivedBytes, long totalBytes);
    public delegate void DownloadStatusChangedEventHandler(string resourcePath);
    public delegate void JavascriptContextCreatedEventHandler(string frameName);
    public delegate void UnhandledAsyncExceptionEventHandler(UnhandledAsyncExceptionEventArgs eventArgs);
    public delegate void FilesDraggingEventHandler(string[] fileNames);
    public delegate void TextDraggingEventHandler(string textContent);

    internal delegate void JavacriptDialogShowEventHandler(string text, Action closeDialog);
    internal delegate void JavascriptContextReleasedEventHandler(string frameName);
    internal delegate void KeyPressedEventHandler(KeyEventDetails keyEvent, out bool handled);

    public partial class WebView : IDisposable {

        private const string MainFrameName = null;

        private object SyncRoot { get; } = new object();

        private Dictionary<string, JavascriptExecutor> JsExecutors { get; } = new Dictionary<string, JavascriptExecutor>();

        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        private IWebViewEngine engine;
        private bool isDeveloperToolsOpened;
        private Action pendingInitialization;
        private string htmlToLoad;
        private volatile bool isDisposing;
        private volatile bool isJavascriptContextTrackingStopped;
        private IDisposable[] disposables;

        private CancellationTokenSource AsyncCancellationTokenSource { get; } = new CancellationTokenSource();

        public event Action WebViewInitialized;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event BeforeNavigateEventHandler BeforeNavigate;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event BeforeResourceLoadEventHandler BeforeResourceLoad;

        public event NavigatedEventHandler Navigated;
        public event LoadFailedEventHandler LoadFailed;
        public event ResourceLoadFailedEventHandler ResourceLoadFailed;
        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public event DownloadStatusChangedEventHandler DownloadCompleted;
        public event DownloadStatusChangedEventHandler DownloadCancelled;
        public event JavascriptContextCreatedEventHandler JavascriptContextCreated;
        public event Action TitleChanged;
        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException;
        public event Action</*url*/string> PopupOpening;

        internal event Action Disposed;
        internal event JavascriptContextReleasedEventHandler JavascriptContextReleased;
        internal event JavacriptDialogShowEventHandler JavacriptDialogShown;
        internal event FilesDraggingEventHandler FilesDragging;
        internal event TextDraggingEventHandler TextDragging;
        internal event KeyPressedEventHandler KeyPressed;

        private static int domainId = 1;

        // cef maints same zoom level for all browser instances under the same domain
        // having different domains will prevent synced zoom
        private string CurrentDomainId { get; }

        private string DefaultLocalUrl { get; }

        /// <summary>
        /// Executed when a web view is initialized. Can be used to attach or configure the webview before it's ready.
        /// </summary>
        public static event Action<WebView> GlobalWebViewInitialized;

        public static GlobalSettings Settings { get; } = new GlobalSettings();

        public WebView() : this(false) { }

        /// <param name="useSharedDomain">Shared domains means that the webview default domain will always be the same. When <paramref ref="useSharedDomain"/> is false a
        /// unique domain is used for every webview.</param>
        internal WebView(bool useSharedDomain) {
            if (IsInDesignMode) {
                return;
            }

            if (useSharedDomain) {
                CurrentDomainId = string.Empty;
            } else {
                CurrentDomainId = domainId.ToString();
                domainId++;
            }

            DefaultLocalUrl = UrlHelper.DefaultLocalUrl.WithDomain(CurrentDomainId);

            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize() {
            WebViewLoader.Initialize(Settings);

            engine = CreateEngine();

            EditCommands = new EditCommands(engine);

            disposables = new IDisposable[] {
                AsyncCancellationTokenSource,
                engine // browser should be the last
            };

            ExtraInitialize();

            GlobalWebViewInitialized?.Invoke(this);
        }

        partial void ExtraInitialize();

        /// <summary>
        /// Creates the engine this build was compiled with (see the WebViewEngine build property).
        /// </summary>
        private partial IWebViewEngine CreateEngine();

        ~WebView() {
            InnerDispose();
        }

        public void Dispose() {
            InnerDispose();
            GC.SuppressFinalize(this);
        }

        private void InnerDispose() {
            lock (SyncRoot) {
                if (isDisposing) {
                    return;
                }
                isDisposing = true;
            }

            var disposed = false;

            void InternalDispose() {
                if (disposed) {
                    return; // bail-out
                }

                disposed = true;

                AsyncCancellationTokenSource?.Cancel();

                // stop listening to javascript context notifications from the engine
                isJavascriptContextTrackingStopped = true;

                WebViewInitialized = null;
                BeforeNavigate = null;
                BeforeResourceLoad = null;
                Navigated = null;
                LoadFailed = null;
                DownloadProgressChanged = null;
                DownloadCompleted = null;
                DownloadCancelled = null;
                JavascriptContextCreated = null;
                TitleChanged = null;
                UnhandledAsyncException = null;
                JavascriptContextReleased = null;

                // dispose the js executors before the browser to prevent (the browser) from throwing cancellation exceptions
                DisposeJavascriptExecutors();
                
                foreach (var disposable in disposables) {
                    disposable?.Dispose();
                }

                Disposed?.Invoke();
            }

            if (JavascriptPendingCalls?.CurrentCount > 1) {
                // avoid dead-lock, wait for all pending calls to finish
                Task.Run(() => {
                    JavascriptPendingCalls?.Signal(); // remove dummy entry
                    JavascriptPendingCalls?.Wait();
                    InternalDispose();
                });
                return;
            }

            InternalDispose();
        }

        internal IWebViewEngine Engine => engine;

        internal bool IsDisposing => isDisposing;

        public bool AllowDeveloperTools { get; set; }

        private string InternalAddress {
            get { return engine.Address; }
            set {
                if (engine.Address != value) {
                    LoadUrl(value, MainFrameName);
                }
            }
        }

        public EditCommands EditCommands { get; private set; }

        public bool CanGoBack => engine.CanGoBack;

        public bool CanGoForward => engine.CanGoForward;

        public void GoBack() => engine.GoBack();

        public void GoForward() => engine.GoForward();

        public bool IsSecurityDisabled { get; set; }

        public bool IgnoreCertificateErrors { get; set; }

        public bool IsHistoryDisabled { get; set; }

        public TimeSpan? DefaultScriptsExecutionTimeout { get; set; }

        public bool DisableBuiltinContextMenus { get; set; }

        public bool DisableFileDialogs { get; set; }

        public bool IsBrowserInitialized => engine.IsBrowserInitialized;

        public bool IsJavascriptEngineInitialized => engine.IsJavascriptEngineInitialized;

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

        public string Title => engine.Title;

        public double ZoomPercentage {
            get { return engine.ZoomPercentage; }
            set { ExecuteWhenInitialized(() => engine.ZoomPercentage = value); }
        }

        public void ShowDeveloperTools() {
            ExecuteWhenInitialized(() => {
                engine.ShowDeveloperTools();
                isDeveloperToolsOpened = true;
            });
        }

        public void CloseDeveloperTools() {
            if (isDeveloperToolsOpened) {
                engine.CloseDeveloperTools();
                isDeveloperToolsOpened = false;
            }
        }

        private void ToggleDeveloperTools() {
            if (isDeveloperToolsOpened) {
                CloseDeveloperTools();
            } else {
                ShowDeveloperTools();
            }
        }

        public void LoadUrl(string address, string frameName = MainFrameName) {
            if (this.IsMainFrame(frameName) && address != DefaultLocalUrl) {
                htmlToLoad = null;
            }
            if (this.IsMainFrame(frameName)) {
                engine.Navigate(address);
            } else {
                this.GetFrame(frameName)?.LoadUrl(address);
            }
        }

        public void LoadResource(ResourceUrl resourceUrl, string frameName = MainFrameName) {
            LoadUrl(resourceUrl.WithDomain(CurrentDomainId), frameName);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            LoadUrl(DefaultLocalUrl, MainFrameName);
        }

        public void Reload(bool ignoreCache = false) {
            if (IsBrowserInitialized && !engine.IsLoading) {
                engine.Reload(ignoreCache);
            }
        }

        internal void HandleBrowserInitialized() {
            if (IsBrowserInitialized) {
                AsyncExecuteInUI(() => {
                    lock (SyncRoot) {
                        if (pendingInitialization != null) {
                            pendingInitialization();
                            pendingInitialization = null;
                        }
                    }
                    WebViewInitialized?.Invoke();
                });
            } else {
                Dispose();
            }
        }

        internal void HandleLoadEnd(IJavascriptFrame frame) {
            var url = frame.Url;
            if (UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (frame.IsMain && url == DefaultLocalUrl) {
                // finished loading local url, discard html
                htmlToLoad = null;
            } else {
                // js context created event is not called for child frames
                HandleJavascriptContextCreated(frame);
            }
            if (Navigated != null) {
                // store frame name and url beforehand (cannot do it later, since frame might be disposed)
                var frameName = frame.Name;
                AsyncExecuteInUI(() => Navigated?.Invoke(url, frameName));
            }
        }

        /// <param name="isAborted">True when the load was aborted (eg: by a new navigation).</param>
        internal void HandleLoadError(string failedUrl, int errorCode, bool isAborted, string frameName, bool isMainFrame) {
            var url = failedUrl;
            if (UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (isMainFrame && url == DefaultLocalUrl) {
                // failed loading default local url, discard html
                htmlToLoad = null;
            }
            if (!isAborted && LoadFailed != null) {
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                AsyncExecuteInUI(() => LoadFailed?.Invoke(url, errorCode, frameName));
            }
        }

        private void ExecuteWhenInitialized(Action action) {
            if (IsBrowserInitialized) {
                action();
            } else {
                lock (SyncRoot) {
                    if (IsBrowserInitialized) {
                        action();
                    } else {
                        pendingInitialization += action;
                    }
                }
            }
        }

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandling(Action action) => ExecuteWithAsyncErrorHandlingOnFrame(action, null);

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandlingOnFrame(Action action, string frameName) {
            try {
                action();
            } catch (Exception e) {
                ForwardUnhandledAsyncException(e, frameName);
            }
        }

        private void ForwardUnhandledAsyncException(Exception e, string frameName = null) {
            if (isDisposing) {
                return;
            }

            var handled = false;

            var unhandledAsyncException = UnhandledAsyncException;
            if (unhandledAsyncException != null) {
                var eventArgs = new UnhandledAsyncExceptionEventArgs(e, frameName);
                unhandledAsyncException(eventArgs);
                handled = eventArgs.Handled;
            }

            if (!handled) {
                ForwardException(ExceptionDispatchInfo.Capture(e));
            }
        }

        private JavascriptExecutor GetJavascriptExecutor(string frameName) {
            if (isDisposing) {
                return null;
            }

            lock (JsExecutors) {
                if (isDisposing) {
                    return null;
                }

                var frameNameForIndex = frameName ?? "";
                if (!JsExecutors.TryGetValue(frameNameForIndex, out var jsExecutor)) {
                    jsExecutor = new JavascriptExecutor(this, this.GetFrame(frameName));
                    JsExecutors.Add(frameNameForIndex, jsExecutor);
                }
                return jsExecutor;
            }
        }

        internal void HandleJavascriptContextCreated(IJavascriptFrame frame) {
            if (isDisposing || isJavascriptContextTrackingStopped) {
                return;
            }

            ExecuteWithAsyncErrorHandling(() => {
                if (UrlHelper.IsChromeInternalUrl(frame.Url)) {
                    return;
                }

                lock (JsExecutors) {
                    if (isDisposing) {
                        return;
                    }

                    var frameName = frame.Name;

                    if (frame.IsMain) {
                        // when a new main frame in created, dispose all running executors -> since they should not be valid anymore
                        // all child iframes were gone
                        DisposeJavascriptExecutors(JsExecutors.Where(je => !je.Value.IsValid).Select(je => je.Key).ToArray());
                    }

                    var jsExecutor = GetJavascriptExecutor(frameName);
                    jsExecutor?.StartFlush(frame);

                    JavascriptContextCreated?.Invoke(frameName);
                }
            });
        }

        internal void HandleJavascriptContextReleased(string frameName, string frameUrl) {
            if (isJavascriptContextTrackingStopped) {
                return;
            }

            ExecuteWithAsyncErrorHandling(() => {
                if (UrlHelper.IsChromeInternalUrl(frameUrl)) {
                    return;
                }

                lock (JsExecutors) {
                    DisposeJavascriptExecutors(new[] { frameName });
                }

                JavascriptContextReleased?.Invoke(frameName);
            });
        }

        internal void HandleJavascriptUncaughtException(string message, JavascriptStackFrame[] stackFrames, string frameName) {
            if (JavascriptExecutor.IsInternalException(message)) {
                // ignore internal exceptions, they will be handled by the EvaluateScript caller
                return;
            }
            var javascriptException = new JavascriptException(message, stackFrames);
            ForwardUnhandledAsyncException(javascriptException, frameName);
        }

        internal void HandleUnhandledException(Exception exception) => ForwardUnhandledAsyncException(exception);

        internal void HandleTitleChanged() => TitleChanged?.Invoke();

        private void HandleRenderProcessCrashed() => DisposeJavascriptExecutors();

        internal void HandleRenderProcessTerminated(RenderProcessTerminationStatus status) {
            HandleRenderProcessCrashed();

            const string ExceptionPrefix = "WebView render process ";

            Exception exception;

            switch (status) {
                case RenderProcessTerminationStatus.Crashed:
                    exception = new RenderProcessCrashedException(ExceptionPrefix + "crashed");
                    break;
                case RenderProcessTerminationStatus.Killed:
                    exception = new RenderProcessKilledException(ExceptionPrefix + "was killed", IsDisposing);
                    break;
                case RenderProcessTerminationStatus.OutOfMemory:
                    exception = new RenderProcessOutOfMemoryException(ExceptionPrefix + "ran out of memory");
                    break;
                default:
                    exception = new RenderProcessCrashedException(ExceptionPrefix + "terminated with an unknown reason");
                    break;
            }

            ForwardUnhandledAsyncException(exception);
        }

        /// <summary>
        /// Handles a navigation about to happen.
        /// </summary>
        /// <returns>True when the navigation must be cancelled.</returns>
        internal bool HandleBeforeNavigate(IRequestDetails request, bool isBackOrForwardNavigation) {
            if (UrlHelper.IsInternalUrl(request.Url)) {
                return false;
            }

            if (IsHistoryDisabled && isBackOrForwardNavigation) {
                return true;
            }

            var cancel = false;
            var beforeNavigate = BeforeNavigate;
            if (beforeNavigate != null) {
                var wrappedRequest = new Request(request, GetRequestUrl(request.Url, request.ResourceType));
                ExecuteWithAsyncErrorHandling(() => beforeNavigate(wrappedRequest));
                cancel = wrappedRequest.Canceled;
            }

            return cancel;
        }

        /// <summary>
        /// Handles a resource request intercepted by the engine (other than the html loaded with <see cref="LoadHtml"/>).
        /// </summary>
        /// <returns>The resource handler. When its <see cref="ResourceHandler.Handler"/> is null, nobody responded to the request and the engine should
        /// load it as usual. Otherwise the response is ready once <see cref="IResourceResponse.Continue"/> is called.</returns>
        internal ResourceHandler HandleResourceRequest(IRequestDetails request, Func<IResourceResponse> responseFactory) {
            var resourceHandler = new ResourceHandler(request, GetRequestUrl(request.Url, request.ResourceType), responseFactory);

            void TriggerBeforeResourceLoadEvent() {
                var beforeResourceLoad = BeforeResourceLoad;
                if (beforeResourceLoad != null) {
                    ExecuteWithAsyncErrorHandling(() => beforeResourceLoad(resourceHandler));
                }
            }

            if (Uri.TryCreate(resourceHandler.Url, UriKind.Absolute, out var url) && url.Scheme == ResourceUrl.EmbeddedScheme) {
                resourceHandler.BeginAsyncResponse(() => {
                    var urlWithoutQuery = new UriBuilder(url);
                    if (!string.IsNullOrEmpty(url.Query)) {
                        urlWithoutQuery.Query = string.Empty;
                    }

                    ExecuteWithAsyncErrorHandling(() => resourceHandler.LoadEmbeddedResource(urlWithoutQuery.Uri));

                    TriggerBeforeResourceLoadEvent();

                    if (resourceHandler.Handled || IgnoreMissingResources) {
                        return;
                    }

                    var resourceLoadFailed = ResourceLoadFailed;
                    if (resourceLoadFailed != null) {
                        resourceLoadFailed(url.ToString());
                    } else {
                        ForwardUnhandledAsyncException(new InvalidOperationException("Resource not found: " + url));
                    }
                });
            } else {
                TriggerBeforeResourceLoadEvent();
            }

            return resourceHandler;
        }

        /// <summary>
        /// Returns the html to serve for the url, when it is the url of the html loaded with <see cref="LoadHtml"/>.
        /// </summary>
        internal bool TryGetHtmlToLoad(string url, out string html) {
            if (url == DefaultLocalUrl) {
                html = htmlToLoad ?? "";
                return true;
            }
            html = null;
            return false;
        }

        /// <summary>
        /// Handles a request to open a popup window.
        /// </summary>
        /// <returns>True when the popup was handled and the engine must not open it.</returns>
        internal bool HandleBeforePopup(string targetUrl, string sourceFrameUrl) {
            if (UrlHelper.IsChromeInternalUrl(targetUrl)) {
                return false;
            }

            if (Uri.IsWellFormedUriString(targetUrl, UriKind.RelativeOrAbsolute)) {
                var uri = new Uri(targetUrl);
                if (!uri.IsAbsoluteUri) {
                    // turning relative urls into full path to avoid that someone runs custom command lines
                    targetUrl = new Uri(new Uri(sourceFrameUrl), uri).AbsoluteUri;
                }
            } else {
                return false; // if the url is not well formed let's use the browser to handle the things
            }

            try {
                var popupOpening = PopupOpening;
                if (popupOpening != null) {
                    popupOpening(targetUrl);
                } else {
                    UrlHelper.OpenInExternalBrowser(targetUrl);
                }
            } catch {
                // if we can't handle the command line let's continue the normal request with the popup
                // with this, will not blow in the users face
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles a javascript dialog (eg: alert).
        /// </summary>
        /// <returns>True when the dialog was handled, in which case <paramref name="closeDialog"/> is called when the dialog closes.</returns>
        internal bool HandleJavascriptDialog(string messageText, Action closeDialog) {
            var javacriptDialogShown = JavacriptDialogShown;
            if (javacriptDialogShown == null) {
                return false;
            }

            javacriptDialogShown.Invoke(messageText, closeDialog);
            return true;
        }

        internal bool HandleKeyPressed(KeyEventDetails keyEvent) {
            var handler = KeyPressed;
            if (handler == null) {
                return false;
            }
            handler(keyEvent, out var handled);
            return handled;
        }

        internal bool HasKeyPressedHandler => KeyPressed != null;

        internal void HandleGotFocus() => OnGotFocus();

        internal void HandleLostFocus() => OnLostFocus();

        private void DisposeJavascriptExecutors() {
            lock (JsExecutors) {
                DisposeJavascriptExecutors(JsExecutors.Keys.ToArray());
            }
        }

        private void DisposeJavascriptExecutors(string[] executorsKeys) {
            foreach (var executorKey in executorsKeys) {
                var indexedExecutorKey = executorKey ?? "";
                if (JsExecutors.TryGetValue(indexedExecutorKey, out var executor)) {
                    executor.Dispose();
                    JsExecutors.Remove(indexedExecutorKey);
                }
            }
        }

        protected virtual string GetRequestUrl(string url, ResourceType resourceType) => url;

        /// <summary>
        /// Called when the webview has received focus.
        /// </summary>
        protected virtual void OnGotFocus() { }

        /// <summary>
        /// Called when the webview is about to loose focus. For instance, if
        /// focus was on the last HTML element and the user pressed the TAB key.
        /// </summary>
        protected virtual void OnLostFocus() { }
        
        /// <summary>
        /// Enables or disable support for accessibility tools like screen readers.
        /// </summary>
        public void EnableAccessibility(bool enable) =>
            ExecuteWhenInitialized(() => engine.SetAccessibilityState(enable));
    }
}