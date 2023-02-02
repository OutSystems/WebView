using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Events;

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
    internal delegate void KeyPressedEventHandler(CefKeyEvent keyEvent, out bool handled);

    public partial class WebView : IDisposable {

        private const string MainFrameName = null;

        // converts cef zoom percentage to css zoom (between 0 and 1)
        // from https://code.google.com/p/chromium/issues/detail?id=71484
        private const float PercentageToZoomFactor = 1.2f;

        private object SyncRoot { get; } = new object();

        private Dictionary<string, JavascriptExecutor> JsExecutors { get; } = new Dictionary<string, JavascriptExecutor>();

        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        private ChromiumBrowser chromium;
        private bool isDeveloperToolsOpened;
        private Action pendingInitialization;
        private string htmlToLoad;
        private volatile bool isDisposing;
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

            chromium = new ChromiumBrowser();
            chromium.BrowserInitialized += OnWebViewBrowserInitialized;
            chromium.LoadEnd += OnWebViewLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += delegate { TitleChanged?.Invoke(); };
            chromium.JavascriptContextCreated += OnJavascriptContextCreated;
            chromium.JavascriptContextReleased += OnJavascriptContextReleased;
            chromium.JavascriptUncaughException += OnJavascriptUncaughException;
            chromium.UnhandledException += (o, e) => ForwardUnhandledAsyncException(e.Exception);

            chromium.RequestHandler = new InternalRequestHandler(this);
            chromium.LifeSpanHandler = new InternalLifeSpanHandler(this);
            chromium.ContextMenuHandler = new InternalContextMenuHandler(this);
            chromium.DialogHandler = new InternalDialogHandler(this);
            chromium.DownloadHandler = new InternalDownloadHandler(this);
            chromium.JSDialogHandler = new InternalJsDialogHandler(this);
            chromium.DragHandler = new InternalDragHandler(this);
            chromium.KeyboardHandler = new InternalKeyboardHandler(this);

            if (!Settings.OsrEnabled) {
                // having the handler (by default) seems to cause some focus troubles, enable only osr disabled
                chromium.FocusHandler = new InternalFocusHandler(this);
            }

            EditCommands = new EditCommands(chromium);

            disposables = new IDisposable[] {
                AsyncCancellationTokenSource,
                chromium // browser should be the last
            };

            ExtraInitialize();

            GlobalWebViewInitialized?.Invoke(this);
        }

        partial void ExtraInitialize();

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

                chromium.JavascriptContextCreated -= OnJavascriptContextCreated;
                chromium.JavascriptContextReleased -= OnJavascriptContextReleased;

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

        internal ChromiumBrowser UnderlyingBrowser => chromium;

        internal bool IsDisposing => isDisposing;

        public bool AllowDeveloperTools { get; set; }

        private string InternalAddress {
            get { return chromium.Address; }
            set {
                if (chromium.Address != value) {
                    LoadUrl(value, MainFrameName);
                }
            }
        }

        public EditCommands EditCommands { get; private set; }

        public bool CanGoBack => chromium.CanGoBack;

        public bool CanGoForward => chromium.CanGoForward;

        public void GoBack() => chromium.GoBack();

        public void GoForward() => chromium.GoForward();

        public bool IsSecurityDisabled { get; set; }

        public bool IgnoreCertificateErrors { get; set; }

        public bool IsHistoryDisabled { get; set; }

        public TimeSpan? DefaultScriptsExecutionTimeout { get; set; }

        public bool DisableBuiltinContextMenus { get; set; }

        public bool DisableFileDialogs { get; set; }

        public bool IsBrowserInitialized => chromium.IsBrowserInitialized;

        public bool IsJavascriptEngineInitialized => chromium.IsJavascriptEngineInitialized;

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

        public string Title => chromium.Title;

        public double ZoomPercentage {
            get { return Math.Pow(PercentageToZoomFactor, chromium.ZoomLevel); }
            set { ExecuteWhenInitialized(() => chromium.ZoomLevel = Math.Log(value, PercentageToZoomFactor)); }
        }

        public void ShowDeveloperTools() {
            ExecuteWhenInitialized(() => {
                chromium.ShowDeveloperTools();
                isDeveloperToolsOpened = true;
            });
        }

        public void CloseDeveloperTools() {
            if (isDeveloperToolsOpened) {
                chromium.CloseDeveloperTools();
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
                chromium.Address = address;
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
            if (IsBrowserInitialized && !chromium.IsLoading) {
                chromium.Reload(ignoreCache);
            }
        }

        private void OnWebViewBrowserInitialized() {
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

        private void OnWebViewLoadEnd(object sender, LoadEndEventArgs e) {
            var url = e.Frame.Url;
            if (UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (e.Frame.IsMain && url == DefaultLocalUrl) {
                // finished loading local url, discard html
                htmlToLoad = null;
            } else {
                // js context created event is not called for child frames
                HandleJavascriptContextCreated(e.Frame);
            }
            if (Navigated != null) {
                // store frame name and url beforehand (cannot do it later, since frame might be disposed)
                var frameName = e.Frame.Name;
                AsyncExecuteInUI(() => Navigated?.Invoke(url, frameName));
            }
        }

        private void OnWebViewLoadError(object sender, LoadErrorEventArgs e) {
            var url = e.FailedUrl;
            if (UrlHelper.IsChromeInternalUrl(url)) {
                return;
            }

            if (e.Frame.IsMain && url == DefaultLocalUrl) {
                // failed loading default local url, discard html
                htmlToLoad = null;
            }
            if (e.ErrorCode != CefErrorCode.Aborted && LoadFailed != null) {
                var frameName = e.Frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                AsyncExecuteInUI(() => LoadFailed?.Invoke(url, (int)e.ErrorCode, frameName));
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

        private void OnJavascriptContextCreated(object sender, JavascriptContextLifetimeEventArgs e) => HandleJavascriptContextCreated(e.Frame);

        private void HandleJavascriptContextCreated(CefFrame frame) {
            if (isDisposing) {
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

                    if (this.IsMainFrame(frameName)) {
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

        private void OnJavascriptContextReleased(object sender, JavascriptContextLifetimeEventArgs e) {
            ExecuteWithAsyncErrorHandling(() => {
                if (UrlHelper.IsChromeInternalUrl(e.Frame.Url)) {
                    return;
                }

                var frameName = e.Frame.Name;

                lock (JsExecutors) {
                    DisposeJavascriptExecutors(new[] { frameName });
                }

                JavascriptContextReleased?.Invoke(frameName);
            });
        }

        private void OnJavascriptUncaughException(object sender, JavascriptUncaughtExceptionEventArgs e) {
            if (JavascriptExecutor.IsInternalException(e.Message)) {
                // ignore internal exceptions, they will be handled by the EvaluateScript caller
                return;
            }
            var javascriptException = new JavascriptException(e.Message, e.StackFrames);
            ForwardUnhandledAsyncException(javascriptException, e.Frame.Name);
        }

        private void HandleRenderProcessCrashed() => DisposeJavascriptExecutors();

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
    }
}