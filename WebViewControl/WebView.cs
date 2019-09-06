using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Events;

// TODO remvoe all cefsharp references

namespace WebViewControl {

    public delegate void BeforeNavigateEventHandler(Request request);
    public delegate void BeforeResourceLoadEventHandler(ResourceHandler resourceHandler);
    public delegate void NavigatedEventHandler(string url, string frameName);
    public delegate void LoadFailedEventHandler(string url, int errorCode, string frameName);
    public delegate void ResourceLoadFailedEventHandler(string resourceUrl);
    public delegate void DownloadProgressChangedEventHandler(string resourcePath, long receivedBytes, long totalBytes);
    public delegate void DownloadStatusChangedEventHandler(string resourcePath);
    public delegate void JavascriptContextCreatedEventHandler(string frameName);
    internal delegate void JavascriptContextReleasedEventHandler(string frameName);
    public delegate void UnhandledAsyncExceptionEventHandler(UnhandledAsyncExceptionEventArgs eventArgs);

    public partial class WebView : UserControl, IDisposable {

        internal const string MainFrameName = null;

        private static string[] CustomSchemes { get; } = new[] {
            ResourceUrl.LocalScheme,
            ResourceUrl.EmbeddedScheme,
            ResourceUrl.CustomScheme
        };

        // converts cef zoom percentage to css zoom (between 0 and 1)
        // from https://code.google.com/p/chromium/issues/detail?id=71484
        private const float PercentageToZoomFactor = 1.2f;

        private static bool subscribedApplicationExit = false;

        private object SyncRoot { get; } = new object();

        private Dictionary<string, JavascriptExecutor> JsExecutors { get; } = new Dictionary<string, JavascriptExecutor>();

        private ChromiumBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private Action pendingInitialization;
        private string htmlToLoad;
        private volatile bool isDisposing;
        private volatile int javascriptPendingCalls;
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

        private event Action RenderProcessCrashed;
        private event Action JavascriptCallFinished;

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
            if (CefRuntimeLoader.IsInitialized) {
                return;
            }

            var cefSettings = new CefSettings();
            cefSettings.LogSeverity = string.IsNullOrWhiteSpace(LogFile) ? CefLogSeverity.Disable : (EnableErrorLogOnly ? CefLogSeverity.Error : CefLogSeverity.Verbose);
            cefSettings.LogFile = LogFile;
            cefSettings.UncaughtExceptionStackSize = 100; // enable stack capture
            cefSettings.CachePath = CachePath; // enable cache for external resources to speedup loading

            // TODO cefSettings.BrowserSubprocessPath = CefLoader.GetBrowserSubProcessPath();

            var customSchemes = CustomSchemes.Select(s => new CustomScheme() { SchemeName = s, SchemeHandlerFactory = new SchemeHandlerFactory() }).ToArray();

            CefRuntimeLoader.Initialize(new string[0], cefSettings, customSchemes);

            if (Application.Current != null) {
                Application.Current.Exit += OnApplicationExit;
                subscribedApplicationExit = true;
            }
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

        public WebView() {
            if (DesignerProperties.GetIsInDesignMode(this)) {
                return;
            }

#if DEBUG
            if (!System.Diagnostics.Debugger.IsAttached) {
                throw new InvalidOperationException("Running debug version");
            }
#endif

            if (UseSharedDomain) {
                CurrentDomainId = string.Empty;
            } else {
                CurrentDomainId = domainId.ToString();
                domainId++;
            }

            DefaultLocalUrl = new ResourceUrl(ResourceUrl.LocalScheme, "index.html").WithDomain(CurrentDomainId);

            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize() {
            InitializeCef();

            if (!subscribedApplicationExit) {
                // subscribe exit again, first time might have failed if Application.Current was null
                Application.Current.Exit += OnApplicationExit;
                subscribedApplicationExit = true;
            }

            var requestHandler = new InternalRequestHandler(this);

            disposables = new IDisposable[] {
                requestHandler
            };

            chromium = new ChromiumBrowser();
            chromium.BrowserInitialized += OnWebViewBrowserInitialized;
            chromium.LoadEnd += OnWebViewLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += OnWebViewTitleChanged;
            chromium.PreviewKeyDown += OnPreviewKeyDown;
            chromium.JavascriptContextCreated += OnJavascriptContextCreated;
            chromium.JavascriptContextReleased += OnJavascriptContextReleased;
            
            chromium.RequestHandler = requestHandler;
            chromium.LifeSpanHandler = new InternalLifeSpanHandler(this);
            chromium.ContextMenuHandler = new InternalContextMenuHandler(this);
            chromium.DialogHandler = new InternalDialogHandler(this);
            chromium.DownloadHandler = new InternalDownloadHandler(this);

            RegisterJavascriptObject(Listener.EventListenerObjName, EventsListener);

            Content = chromium;

            GlobalWebViewInitialized?.Invoke(this);

            RenderProcessCrashed += OnRenderProcessCrashed;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, FocusableElement);
        }

        private static void OnApplicationExit(object sender, ExitEventArgs e) {
            Cleanup();
        }

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
                RenderProcessCrashed = null;
                JavascriptContextReleased = null;

                foreach (var disposable in disposables.Concat(JsExecutors.Values)) {
                    disposable.Dispose();
                }

                chromium.Dispose();
                AsyncCancellationTokenSource.Dispose();

                Disposed?.Invoke();
            }

            // avoid dead-lock, wait for all pending calls to finish
            JavascriptCallFinished += () => {
                if (javascriptPendingCalls == 0) {
                    Dispatcher.BeginInvoke((Action)InternalDispose);
                }
            };

            if (javascriptPendingCalls > 0) {
                // JavascriptCallFinished event will trigger InternalDispose, 
                // this check must come after registering event to avoid losing the event call
                return;
            }

            InternalDispose();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                if (isDeveloperToolsOpened) {
                    CloseDeveloperTools();
                } else {
                    ShowDeveloperTools();
                }
                e.Handled = true;
            }
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
            if (frameName == MainFrameName && address != DefaultLocalUrl) {
                htmlToLoad = null;
            }
            if (address.Contains(Uri.SchemeDelimiter) || address == UrlHelper.AboutBlankUrl || address.StartsWith("data:")) {
                if (CustomSchemes.Any(s => address.StartsWith(s + Uri.SchemeDelimiter))) {
                    // custom schemes -> turn off security ... to enable full access without problems to local resources
                    IsSecurityDisabled = true;
                } else {
                    IsSecurityDisabled = false;
                }

                chromium.Address = address;
                // must wait for the browser to be initialized otherwise navigation will be aborted
                //ExecuteWhenInitialized(() => {
                //    if (frameName == MainFrameName) {
                        
                //    } else {
                //        GetFrame(frameName)?.LoadUrl(address);
                //    }
                //});
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
                return RegisterJavascriptObject(name, objectToBind, target => Dispatcher.Invoke(target), false);
            }
            
            if (interceptCall == null) {
                interceptCall = target => target();
            }

            object CallTargetMethod(Func<object> target) {
                if (isDisposing) {
                    return null;
                }
                try {
                    javascriptPendingCalls++;
                    if (isDisposing) {
                        // check again, to avoid concurrency problems with dispose
                        return null;
                    }
                    return interceptCall(target);
                } finally {
                    javascriptPendingCalls--;
                    JavascriptCallFinished?.Invoke();
                }
            }

            chromium.RegisterJavascriptObject(objectToBind, name, CallTargetMethod);
        
            return true;
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
            if (chromium.IsBrowserInitialized && !chromium.IsLoading) {
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
                        Dispatcher.BeginInvoke(handler, new object[] { args });
                    } else {
                        ExecuteWithAsyncErrorHandling(handler, new object[] { args });
                    }
                }
            }

            return new Listener(name, HandleEvent, EventsListener);
        }

        private void OnWebViewBrowserInitialized() {
            if (chromium.IsBrowserInitialized) {
                if (pendingInitialization != null) {
                    pendingInitialization();
                    pendingInitialization = null;
                }
                WebViewInitialized?.Invoke();
            } else {
                Dispose();
            }
        }

        private void OnWebViewLoadEnd(object sender, LoadEndEventArgs e) {
            if (e.Frame.IsMain) {
                htmlToLoad = null;
            } else {
                // js context created event is not called for child frames
                // TODO OnJavascriptContextCreated(e.Frame.Name);
            }
            var navigated = Navigated;
            if (navigated != null) {
                // store frame name and url beforehand (cannot do it later, since frame might be disposed)
                var url = e.Frame.Url;
                var frameName = e.Frame.Name; 
                AsyncExecuteInUI(() => navigated(url, frameName));
            }
        }

        private void OnWebViewLoadError(object sender, LoadErrorEventArgs e) {
            if (e.Frame.IsMain) {
                htmlToLoad = null;
            }
            var loadFailed = LoadFailed;
            if (e.ErrorCode != CefErrorCode.Aborted && loadFailed != null) {
                var frameName = e.Frame.Name; // store frame name beforehand (cannot do it later, since frame might be disposed)
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                AsyncExecuteInUI(() => loadFailed(e.FailedUrl, (int)e.ErrorCode, frameName));
            }
        }

        private void OnWebViewTitleChanged(object sender, string title) {
            TitleChanged?.Invoke();
        }

        public static void SetCookie(string url, string domain, string name, string value, DateTime expires) {
            var cookie = new CefCookie() {
                Domain = domain,
                Name = name,
                Value = value,
                Expires = expires
            };
            // TODO CefRuntime.GetGlobalCookieManager().SetCookieAsync(url, cookie);
        }

        public static string CookiesPath {
            set { /* TODO Cef.GetGlobalCookieManager().SetStoragePath(value, true); */ }
        }

        private static bool IsFrameworkAssemblyName(string name) {
            return name == "PresentationFramework" || name == "PresentationCore" || name == "mscorlib" || name == "System.Xaml" || name == "WindowsBase";
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
                pendingInitialization += action;
            }
        }

        private void AsyncExecuteInUI(Action action) {
            if (isDisposing) {
                return;
            }
            // use async call to avoid dead-locks, otherwise if the source action tries to to evaluate js it would block
            Dispatcher.InvokeAsync(
                () => {
                    if (!isDisposing) {
                        ExecuteWithAsyncErrorHandling(action);
                    }
                },
                DispatcherPriority.Normal,
                AsyncCancellationTokenSource.Token);
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
                var exceptionInfo = ExceptionDispatchInfo.Capture(e);
                // don't use invoke async, as it won't forward the exception to the dispatcher unhandled exception event
                Dispatcher.BeginInvoke((Action)(() => {
                    if (!isDisposing) {
                        exceptionInfo?.Throw();
                    }
                }));
            }
        }

        internal IInputElement FocusableElement {
            get { return chromium; }
        }

        protected void InitializeBrowser() {
            chromium.CreateBrowser();
        }

        public static string LogFile { get; set; }

        public static string CachePath { get; set; } = Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);

        public static bool PersistCache { get; set; } = false;

        public static bool EnableErrorLogOnly { get; set; } = false;

        internal bool IsDisposing => isDisposing;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            // IWindowService is a WPF internal property set when component is loaded into a new window, even if the window isn't shown
            if (e.Property.Name == "IWindowService") {
                if (e.OldValue is Window oldWindow) {
                    oldWindow.Closed -= OnHostWindowClosed;
                }

                if (e.NewValue is Window newWindow) {
                    newWindow.Closed += OnHostWindowClosed;
                }
            }
        }

        private void OnHostWindowClosed(object sender, EventArgs e) {
            ((Window)sender).Closed -= OnHostWindowClosed;
            Dispose();
        }

        // TODO
        //protected void RegisterProtocolHandler(string protocol, CefResourceHandlerFactory handler) {
        //    if (chromium.RequestContext == null) {
        //        chromium.RequestContext = new RequestContext(new RequestContextSettings() {
        //            CachePath = CachePath
        //        });
        //    }
        //    chromium.RequestContext.RegisterSchemeHandlerFactory(protocol, "", handler);
        //}

        protected virtual string GetRequestUrl(string url, ResourceType resourceType) {
            return url;
        }

        protected virtual bool UseSharedDomain => false;

        public string[] GetFrameNames() {
            var browser = chromium.GetBrowser();
            return browser?.GetFrameNames().Where(n => n != MainFrameName).ToArray() ?? new string[0];
        }

        internal bool HasFrame(string name) {
            return GetFrame(name) != null;
        }

        private CefFrame GetFrame(string frameName) {
            return chromium.GetBrowser()?.GetFrame(frameName ?? "");
        }

        private JavascriptExecutor GetJavascriptExecutor(string frameName) {
            lock(JsExecutors) {
                var frameNameForIndex = frameName ?? "";
                if (!JsExecutors.TryGetValue(frameNameForIndex, out var jsExecutor)) {
                    jsExecutor = new JavascriptExecutor(this, GetFrame(frameName));
                    JsExecutors.Add(frameNameForIndex, jsExecutor);
                }
                return jsExecutor;
            }
        }

        private void OnJavascriptContextCreated(object sender, JavascriptContextLifetimeEventArgs e) {
            ExecuteWithAsyncErrorHandling(() => {
                if (UrlHelper.IsChromeInternalUrl(e.Frame.Url)) {
                    return;
                }

                lock (JsExecutors) {
                    var frameName = e.Frame.Name;

                    if (frameName == MainFrameName) {
                        // when a new main frame in created, dispose all running executors -> since they should not be valid anymore
                        // all child iframes were gone
                        DisposeJavascriptExecutors(JsExecutors.Where(je => !je.Value.IsValid).Select(je => je.Key).ToArray());
                    }

                    var jsExecutor = GetJavascriptExecutor(frameName);
                    jsExecutor.StartFlush(e.Frame);

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

        private void OnRenderProcessCrashed() {
            lock (JsExecutors) {
                DisposeJavascriptExecutors(JsExecutors.Keys.ToArray());
            }
        }

        private void DisposeJavascriptExecutors(string[] executorsKeys) {
            foreach (var executorKey in executorsKeys) {
                JsExecutors[executorKey].Dispose();
                JsExecutors.Remove(executorKey);
            }
        }

        // TODO
        //protected override void OnUncaughtException(CefBrowser browser, CefFrame frame, CefV8Context context, CefV8Exception exception, CefV8StackTrace stackTrace) {
        //    if (JavascriptExecutor.IsInternalException(exception.Message)) {
        //        // ignore internal exceptions, they will be handled by the EvaluateScript caller
        //        return;
        //    }
        //    var javascriptException = new JavascriptException(exception.Message, exception.StackTrace);
        //    OwnerWebView.ForwardUnhandledAsyncException(javascriptException, frame.Name);
        //}
    }
}
