using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;
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

    internal delegate void JavascriptContextReleasedEventHandler(string frameName);
    internal delegate void JavacriptDialogShowEventHandler(string text, Action closeDialog);

    public partial class WebView : IDisposable {

        private const string AboutBlankUrl = "about:blank";

        private const string MainFrameName = null;

        private static string[] CustomSchemes { get; } = new[] {
            ResourceUrl.LocalScheme,
            ResourceUrl.EmbeddedScheme,
            ResourceUrl.CustomScheme
        };

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

        private BrowserObjectListener EventsListener { get; } = new BrowserObjectListener();
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

        private static int domainId = 1;

        // cef maints same zoom level for all browser instances under the same domain
        // having different domains will prevent synced zoom
        private string CurrentDomainId { get; }

        private string DefaultLocalUrl { get; }

        /// <summary>
        /// Executed when a web view is initialized. Can be used to attach or configure the webview before it's ready.
        /// </summary>
        public static event Action<WebView> GlobalWebViewInitialized;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeCef() {
            if (CefRuntimeLoader.IsLoaded) {
                return;
            }

            var cefSettings = new CefSettings();
            cefSettings.LogSeverity = string.IsNullOrWhiteSpace(LogFile) ? CefLogSeverity.Disable : (EnableErrorLogOnly ? CefLogSeverity.Error : CefLogSeverity.Verbose);
            cefSettings.LogFile = LogFile;
            cefSettings.UncaughtExceptionStackSize = 100; // enable stack capture
            cefSettings.CachePath = CachePath; // enable cache for external resources to speedup loading
            cefSettings.WindowlessRenderingEnabled = OsrEnabled;

            var customSchemes = CustomSchemes.Select(s => new CustomScheme() { SchemeName = s, SchemeHandlerFactory = new SchemeHandlerFactory() }).ToArray();

            CefRuntimeLoader.Initialize(settings: cefSettings, customSchemes: customSchemes);

            AppDomain.CurrentDomain.ProcessExit += delegate { Cleanup(); };
        }

        /// <summary>
        /// Release all resources and shutdown web view
        /// </summary>
        [DebuggerNonUserCode]
        public static void Cleanup() {
            CefRuntime.Shutdown(); // must shutdown cef to free cache files (so that cleanup is able to delete files)

            if (PersistCache) {
                return;
            }

            try {
                var dirInfo = new DirectoryInfo(CachePath);
                if (dirInfo.Exists) {
                    dirInfo.Delete(true);
                }
            } catch (IOException) {
                // ignore
            }
        }

        public WebView() : this(false) { }

        /// <param name="useSharedDomain">Shared domains means that the webview default domain will always be the same. When <see cref="useSharedDomain"/> is false a
        /// unique domain is used for every webview.</param>
        internal WebView(bool useSharedDomain) {
            if (IsInDesignMode) {
                return;
            }

#if DEBUG
            if (!System.Diagnostics.Debugger.IsAttached) {
                throw new InvalidOperationException("Running debug version");
            }
#endif

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
            InitializeCef();

            chromium = new ChromiumBrowser();
            chromium.BrowserInitialized += OnWebViewBrowserInitialized;
            chromium.LoadEnd += OnWebViewLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += OnWebViewTitleChanged;
            chromium.JavascriptContextCreated += OnJavascriptContextCreated;
            chromium.JavascriptContextReleased += OnJavascriptContextReleased;
            chromium.JavascriptUncaughException += OnJavascriptUncaughException;
            chromium.UnhandledException += OnUnhandledException;

            chromium.RequestHandler = new InternalRequestHandler(this);
            chromium.LifeSpanHandler = new InternalLifeSpanHandler(this);
            chromium.ContextMenuHandler = new InternalContextMenuHandler(this);
            chromium.DialogHandler = new InternalDialogHandler(this);
            chromium.DownloadHandler = new InternalDownloadHandler(this);
            chromium.JSDialogHandler = new InternalJsDialogHandler(this);

            disposables = new IDisposable[] {
                chromium,
                AsyncCancellationTokenSource
            };

            RegisterJavascriptObject(Listener.EventListenerObjName, EventsListener);

            ExtraInitialize();

            GlobalWebViewInitialized?.Invoke(this);
        }

        partial void ExtraInitialize();

        ~WebView() {
            Dispose();
        }

        public void Dispose() {
            lock (SyncRoot) {
                if (isDisposing) {
                    return;
                }

                isDisposing = true;
            }

            GC.SuppressFinalize(this);

            var disposed = false;

            void InternalDispose() {
                if (disposed) {
                    return; // bail-out
                }

                disposed = true;

                AsyncCancellationTokenSource.Cancel();

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

                foreach (var disposable in disposables.Concat(JsExecutors.Values)) {
                    disposable.Dispose();
                }

                Disposed?.Invoke();
            }

            if (JavascriptPendingCalls.CurrentCount > 1) {
                // avoid dead-lock, wait for all pending calls to finish
                Task.Run(() => {
                    JavascriptPendingCalls.Signal(); // remove dummy entry
                    JavascriptPendingCalls.Wait();
                    InternalDispose();
                });
                return;
            }

            InternalDispose();
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

        public bool AllowDeveloperTools { get; set; }

        public string Address {
            get { return chromium.Address; }
            set { LoadUrl(value, MainFrameName); }
        }

        public void LoadUrl(string address, string frameName) {
            if (IsMainFrame(frameName) && address != DefaultLocalUrl) {
                htmlToLoad = null;
            }
            if (address.Contains(Uri.SchemeDelimiter) || address == UrlHelper.AboutBlankUrl || address.StartsWith("data:")) {
                if (IsMainFrame(frameName)) {
                    chromium.Address = address;
                } else {
                    GetFrame(frameName)?.LoadUrl(address);
                }
            } else {
                var userAssembly = GetUserCallingMethod().ReflectedType.Assembly;
                LoadUrl(new ResourceUrl(userAssembly, address).WithDomain(CurrentDomainId), frameName);
            }
        }

        public void LoadResource(ResourceUrl resourceUrl, string frameName = MainFrameName) {
            LoadUrl(resourceUrl.WithDomain(CurrentDomainId), frameName);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            LoadUrl(DefaultLocalUrl, MainFrameName);
        }

        public bool IsSecurityDisabled {
            get => chromium.Settings.WebSecurity == CefState.Disabled;
            set {
                if (IsBrowserInitialized) {
                    throw new InvalidOperationException("Cannot change webview settings after initialized");
                }
                chromium.Settings.WebSecurity = (value ? CefState.Disabled : CefState.Enabled);
            }
        }

        public bool IgnoreCertificateErrors { get; set; }

        public bool IsHistoryDisabled { get; set; }

        public TimeSpan? DefaultScriptsExecutionTimeout { get; set; }

        public bool DisableBuiltinContextMenus { get; set; }

        public bool DisableFileDialogs { get; set; }

        public bool IsBrowserInitialized => chromium.IsBrowserInitialized;

        public bool IsJavascriptEngineInitialized => chromium.IsJavascriptEngineInitialized;

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

        /// <summary>
        /// Registers an object with the specified name in the window context of the browser
        /// </summary>
        /// <param name="name"></param>
        /// <param name="objectToBind"></param>
        /// <param name="interceptCall"></param>
        /// <param name="executeCallsInUI"></param>
        /// <returns>True if the object was registered or false if the object was already registered before</returns>
        public bool RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall = null, bool executeCallsInUI = false) {
            if (chromium.IsJavascriptObjectRegistered(name)) {
                return false;
            }

            if (executeCallsInUI) {
                return RegisterJavascriptObject(name, objectToBind, target => ExecuteInUI<object>(target), false);
            }

            if (interceptCall == null) {
                interceptCall = target => target();
            }

            object CallTargetMethod(Func<object> target) {
                if (isDisposing) {
                    return null;
                }
                try {
                    JavascriptPendingCalls.AddCount();
                    if (isDisposing) {
                        // check again, to avoid concurrency problems with dispose
                        return null;
                    }
                    return interceptCall(target);
                } finally {
                    JavascriptPendingCalls.Signal();
                }
            }

            chromium.RegisterJavascriptObject(objectToBind, name, CallTargetMethod);

            return true;
        }

        /// <summary>
        /// Unregisters an object with the specified name in the window context of the browser
        /// </summary>
        /// <param name="name"></param>
        public void UnregisterJavascriptObject(string name) {
            chromium.UnregisterJavascriptObject(name);
        }

        public T EvaluateScript<T>(string script, string frameName = MainFrameName, TimeSpan? timeout = null) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScript<T>(script, timeout: timeout);
            }
            return default(T);
        }

        public void ExecuteScript(string script, string frameName = MainFrameName) {
            GetJavascriptExecutor(frameName)?.ExecuteScript(script);
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            ExecuteScriptFunctionInFrame(functionName, MainFrameName, args);
        }

        public void ExecuteScriptFunctionInFrame(string functionName, string frameName, params string[] args) {
            GetJavascriptExecutor(frameName)?.ExecuteScriptFunction(functionName, false, args);
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return EvaluateScriptFunctionInFrame<T>(functionName, MainFrameName, args);
        }

        public T EvaluateScriptFunctionInFrame<T>(string functionName, string frameName, params string[] args) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScriptFunction<T>(functionName, false, args);
            }
            return default(T);
        }

        internal void ExecuteScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            ExecuteScriptFunctionWithSerializedParamsInFrame(functionName, MainFrameName, args);
        }

        internal void ExecuteScriptFunctionWithSerializedParamsInFrame(string functionName, string frameName, params object[] args) {
            GetJavascriptExecutor(frameName)?.ExecuteScriptFunction(functionName, true, args);
        }

        internal T EvaluateScriptFunctionWithSerializedParams<T>(string functionName, params object[] args) {
            return EvaluateScriptFunctionWithSerializedParamsInFrame<T>(functionName, MainFrameName, args);
        }

        internal T EvaluateScriptFunctionWithSerializedParamsInFrame<T>(string functionName, string frameName, params object[] args) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScriptFunction<T>(functionName, true, args);
            }
            return default(T);
        }

        public bool CanGoBack {
            get { return chromium.CanGoBack; }
        }

        public bool CanGoForward {
            get { return chromium.CanGoForward; }
        }

        public void GoBack() {
            chromium.GoBack();
        }

        public void GoForward() {
            chromium.GoForward();
        }

        public void Reload(bool ignoreCache = false) {
            if (IsBrowserInitialized && !chromium.IsLoading) {
                chromium.Reload(ignoreCache);
            }
        }

        public string Title {
            get { return chromium.Title; }
        }

        public double ZoomPercentage {
            get { return Math.Pow(PercentageToZoomFactor, chromium.ZoomLevel); }
            set { ExecuteWhenInitialized(() => chromium.ZoomLevel = Math.Log(value, PercentageToZoomFactor)); }
        }

        public Listener AttachListener(string name) {
            void HandleEvent(ListenerEventHandler handler, object[] args, bool executeInUI) {
                if (!isDisposing) {
                    if (executeInUI) {
                        AsyncExecuteInUI(() => handler(new object[] { args }));
                    } else {
                        ExecuteWithAsyncErrorHandling(handler, new object[] { args });
                    }
                }
            }

            return new Listener(name, HandleEvent, EventsListener);
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
            var navigated = Navigated;
            if (navigated != null) {
                // store frame name and url beforehand (cannot do it later, since frame might be disposed)
                var frameName = e.Frame.Name;
                AsyncExecuteInUI(() => navigated(url, frameName));
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
            var loadFailed = LoadFailed;
            if (e.ErrorCode != CefErrorCode.Aborted && loadFailed != null) {
                var frameName = e.Frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                AsyncExecuteInUI(() => loadFailed(url, (int)e.ErrorCode, frameName));
            }
        }

        private void OnWebViewTitleChanged(object sender, string title) {
            TitleChanged?.Invoke();
        }

        internal static MethodBase GetUserCallingMethod(bool captureFilenames = false) {
            var currentAssembly = typeof(WebView).Assembly;
            var callstack = new StackTrace(captureFilenames).GetFrames().Select(f => f.GetMethod()).Where(m => m.ReflectedType.Assembly != currentAssembly);
            var userMethod = callstack.First(m => !IsFrameworkAssemblyName(m.ReflectedType.Assembly.GetName().Name));
            if (userMethod == null) {
                throw new InvalidOperationException("Unable to find calling method");
            }
            return userMethod;
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

        private void ExecuteWithAsyncErrorHandling(Action action) {
            ExecuteWithAsyncErrorHandlingOnFrame(action, null);
        }

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandlingOnFrame(Action action, string frameName) {
            try {
                action();
            } catch (Exception e) {
                ForwardUnhandledAsyncException(e, frameName);
            }
        }

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandling(Delegate method, params object[] args) {
            try {
                method.DynamicInvoke(args);
            } catch (Exception e) {
                ForwardUnhandledAsyncException(e.InnerException);
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

        internal void InitializeBrowser() {
            chromium.CreateBrowser();
        }

        public static string LogFile { get; set; }

        public static string CachePath { get; set; } = Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);

        public static bool PersistCache { get; set; } = false;

        public static bool EnableErrorLogOnly { get; set; } = false;

        internal bool IsDisposing => isDisposing;

        protected virtual string GetRequestUrl(string url, ResourceType resourceType) {
            return url;
        }

        public string[] GetFrameNames() {
            var browser = chromium.GetBrowser();
            return browser?.GetFrameNames().Where(n => !IsMainFrame(n)).ToArray() ?? new string[0];
        }

        internal bool HasFrame(string name) {
            return GetFrame(name) != null;
        }

        private CefFrame GetFrame(string frameName) {
            return chromium.GetBrowser()?.GetFrame(frameName ?? "");
        }

        private JavascriptExecutor GetJavascriptExecutor(string frameName) {
            lock (JsExecutors) {
                var frameNameForIndex = frameName ?? "";
                if (!JsExecutors.TryGetValue(frameNameForIndex, out var jsExecutor)) {
                    jsExecutor = new JavascriptExecutor(this, GetFrame(frameName));
                    JsExecutors.Add(frameNameForIndex, jsExecutor);
                }
                return jsExecutor;
            }
        }

        private void OnJavascriptContextCreated(object sender, JavascriptContextLifetimeEventArgs e) {
            HandleJavascriptContextCreated(e.Frame);
        }

        private void HandleJavascriptContextCreated(CefFrame frame) {
            ExecuteWithAsyncErrorHandling(() => {
                if (UrlHelper.IsChromeInternalUrl(frame.Url)) {
                    return;
                }

                lock (JsExecutors) {
                    var frameName = frame.Name;

                    if (IsMainFrame(frameName)) {
                        // when a new main frame in created, dispose all running executors -> since they should not be valid anymore
                        // all child iframes were gone
                        DisposeJavascriptExecutors(JsExecutors.Where(je => !je.Value.IsValid).Select(je => je.Key).ToArray());
                    }

                    var jsExecutor = GetJavascriptExecutor(frameName);
                    jsExecutor.StartFlush(frame);

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

        private void OnUnhandledException(object sender, AsyncUnhandledExceptionEventArgs e) {
            ForwardUnhandledAsyncException(e.Exception);
        }

        private void HandleRenderProcessCrashed() {
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

        private void ToggleDeveloperTools() {
            if (isDeveloperToolsOpened) {
                CloseDeveloperTools();
            } else {
                ShowDeveloperTools();
            }
        }

        internal static bool IsMainFrame(string frameName) {
            return string.IsNullOrEmpty(frameName);
        }
    }
}