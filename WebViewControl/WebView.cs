using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using CefSharp.ModelBinding;

namespace WebViewControl {

    public partial class WebView : ContentControl, IDisposable {

        public const string LocalScheme = "local";

        internal const string EmbeddedScheme = "embedded";

        private static readonly string[] CustomSchemes = new[] {
            LocalScheme,
            EmbeddedScheme
        };

        private static readonly string TempDir = 
            Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);

        private const string ChromeInternalProtocol = "chrome-devtools:";
        
        protected const string DefaultPath = "://webview/";
        private const string DefaultLocalUrl = LocalScheme + DefaultPath + "index.html";
        private const string AssemblyPrefix = EmbeddedScheme + DefaultPath + "assembly:";
        private const string AssemblyPathSeparator = ";";

        // converts cef zoom percentage to css zoom (between 0 and 1)
        // from https://code.google.com/p/chromium/issues/detail?id=71484
        private const float PercentageToZoomFactor = 1.2f;

        private readonly DefaultBinder binder = new DefaultBinder(new DefaultFieldNameConverter());

        private static bool subscribedApplicationExit = false;

        protected InternalChromiumBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private BrowserSettings settings;
        private bool isDisposing;
        private Action pendingInitialization;
        private string htmlToLoad;
        private JavascriptExecutor jsExecutor;
        private CefLifeSpanHandler lifeSpanHandler;
        private volatile int javascriptPendingCalls;

        private readonly BrowserObjectListener eventsListener = new BrowserObjectListener();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public event Action WebViewInitialized;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event Action<Request> BeforeNavigate;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event Action<ResourceHandler> BeforeResourceLoad;

        public event Action<string> Navigated;
        public event Action<string, int> LoadFailed;
        public event Action<string> ResourceLoadFailed;
        public event Action<string, long, long> DownloadProgressChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string> DownloadCancelled;
        public event Action JavascriptContextCreated;
        public event Action TitleChanged;
        public event Action<UnhandledExceptionEventArgs> UnhandledAsyncException;

        private event Action RenderProcessCrashed;
        private event Action JavascriptContextReleased;

        /// <summary>
        /// Executed when a web view is initialized. Can be used to attach or configure the webview before it's ready.
        /// </summary>
        public static event Action<WebView> GlobalWebViewInitialized;

        static WebView() {
            InitializeCef();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeCef() {
            if (!Cef.IsInitialized) {
                var cefSettings = new CefSettings();
                cefSettings.LogSeverity = LogSeverity.Disable; // disable writing of debug.log
                cefSettings.UncaughtExceptionStackSize = 100; // enable stack capture
                cefSettings.CachePath = TempDir; // enable cache for external resources to speedup loading
                CefSharpSettings.LegacyJavascriptBindingEnabled = true;

                foreach (var scheme in CustomSchemes) {
                    cefSettings.RegisterScheme(new CefCustomScheme() {
                        SchemeName = scheme,
                        SchemeHandlerFactory = new CefSchemeHandlerFactory()
                    });
                }

                cefSettings.BrowserSubprocessPath = CefLoader.GetBrowserSubProcessPath();

                Cef.Initialize(cefSettings, performDependencyCheck: false, browserProcessHandler: null);
                
                if (Application.Current != null) {
                    Application.Current.Exit += OnApplicationExit;
                    subscribedApplicationExit = true;
                }
            }
        }

        /// <summary>
        /// Release all resources and shutdown web view
        /// </summary>
        [DebuggerNonUserCode]
        public static void Cleanup() {
            if (!Cef.IsInitialized) {
                return;
            }

            Cef.Shutdown(); // must shutdown cef to free cache files (so that cleanup is able to delete files)
            
            try {
                var dirInfo = new DirectoryInfo(TempDir);
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

            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize() {
            if (!subscribedApplicationExit) {
                // subscribe exit again, first time might have failed if Application.Current was null
                Application.Current.Exit += OnApplicationExit;
                subscribedApplicationExit = true;
            }

            settings = new BrowserSettings();
            lifeSpanHandler = new CefLifeSpanHandler(this);

            chromium = new InternalChromiumBrowser();
            chromium.BrowserSettings = settings;
            chromium.IsBrowserInitializedChanged += OnWebViewIsBrowserInitializedChanged;
            chromium.FrameLoadEnd += OnWebViewFrameLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += OnWebViewTitleChanged;
            chromium.PreviewKeyDown += OnPreviewKeyDown;
            chromium.RequestHandler = new CefRequestHandler(this);
            chromium.ResourceHandlerFactory = new CefResourceHandlerFactory(this);
            chromium.LifeSpanHandler = lifeSpanHandler;
            chromium.RenderProcessMessageHandler = new CefRenderProcessMessageHandler(this);
            chromium.MenuHandler = new CefMenuHandler(this);
            chromium.DialogHandler = new CefDialogHandler(this);
            chromium.DownloadHandler = new CefDownloadHandler(this);
            
            jsExecutor = new JavascriptExecutor(this);

            RegisterJavascriptObject(Listener.EventListenerObjName, eventsListener);

            Content = chromium;
            
            GlobalWebViewInitialized?.Invoke(this);
        }

        private static void OnApplicationExit(object sender, ExitEventArgs e) {
            Cleanup();
        }

        ~WebView() {
            Dispose();
        }

        public void Dispose() {
            if (isDisposing) {
                return;
            }

            isDisposing = true;

            if (javascriptPendingCalls == 0) {
                InternalDispose();
            } else {
                // avoid dead-lock
                Dispatcher.BeginInvoke((Action)InternalDispose);
            }
        }

        private void InternalDispose() {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            cancellationTokenSource.Cancel();
            chromium.RequestHandler = null;
            chromium.ResourceHandlerFactory = null;
            chromium.PreviewKeyDown -= OnPreviewKeyDown;

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

            jsExecutor.Dispose();
            settings.Dispose();
            chromium.Dispose();
            cancellationTokenSource.Dispose();
            settings = null;
            chromium = null;

            GC.SuppressFinalize(this);
        }

        protected override void OnGotFocus(RoutedEventArgs e) {
            base.OnGotFocus(e);
            ExecuteWhenInitialized(() => AsyncExecuteInUI(() => { chromium?.Focus(); }));
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
                chromium.ShowDevTools();
                isDeveloperToolsOpened = true;
            });
        }

        public void CloseDeveloperTools() {
            if (isDeveloperToolsOpened) {
                chromium.CloseDevTools();
                isDeveloperToolsOpened = false;
            }
        }

        public bool AllowDeveloperTools { get; set; }

        public string Address {
            get { return chromium.Address; }
            set {
                if (value != DefaultLocalUrl) {
                    htmlToLoad = null;
                }
                if (value.Contains("://") || value == "about:blank" || value.StartsWith("data:")) {
                    // must wait for the browser to be initialized otherwise navigation will be aborted
                    ExecuteWhenInitialized(() => chromium.Load(value));
                } else {
                    LoadFrom(value);
                }
            }
        }

        public bool IsSecurityDisabled {
            get { return settings.WebSecurity != CefState.Enabled; }
            set { settings.WebSecurity = (value ? CefState.Disabled : CefState.Enabled); }
        }

        public bool IgnoreCertificateErrors {
            get;
            set;
        }

        public bool IsHistoryDisabled {
            get;
            set;
        }

        public TimeSpan? DefaultScriptsExecutionTimeout {
            get;
            set;
        }

        public bool DisableBuiltinContextMenus {
            get;
            set;
        }

        public bool DisableFileDialogs {
            get;
            set;
        }

        public bool IsBrowserInitialized {
            get { return chromium.IsBrowserInitialized; }
        }

        public bool IsJavascriptEngineInitialized {
            get { return chromium.CanExecuteJavascriptInMainFrame; }
        }

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

        protected void LoadFrom(string source) {
            var userAssembly = GetUserCallingMethod().ReflectedType.Assembly;

            IsSecurityDisabled = true;
            Address = BuildEmbeddedResourceUrl(userAssembly, userAssembly.GetName().Name, source);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            Address = DefaultLocalUrl;
        }

        /// <summary>
        /// Registers an object with the specified name in the window context of the browser
        /// </summary>
        /// <param name="name"></param>
        /// <param name="objectToBind"></param>
        /// <param name="interceptCall"></param>
        /// <param name="bind"></param>
        /// <param name="executeCallsInUI"></param>
        /// <returns>True if the object was registered or false if the object was already registered before</returns>
        public bool RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall = null, Func<object, Type, object> bind = null, bool executeCallsInUI = false) {
            if (chromium.JavascriptObjectRepository.IsBound(name)) {
                return false;
            }

            if (executeCallsInUI) {
                Func<Func<object>, object> interceptorWrapper = target => Dispatcher.Invoke(target);
                return RegisterJavascriptObject(name, objectToBind, interceptorWrapper, bind, false);

            } else {
                var bindingOptions = new BindingOptions();
                if (bind != null) {
                    bindingOptions.Binder = new LambdaMethodBinder(bind);
                } else {
                    bindingOptions.Binder = binder;
                }

                Func<Func<object>, object> interceptorWrapper;
                if (interceptCall != null) {
                    interceptorWrapper = target => {
                        if (isDisposing) {
                            return null;
                        }
                        try {
                            javascriptPendingCalls++;
                            return interceptCall(target);
                        } finally {
                            javascriptPendingCalls--;
                        }
                    };
                } else {
                    interceptorWrapper = target => {
                        if (isDisposing) {
                            return null;
                        }
                        try {
                            javascriptPendingCalls++;
                            return target();
                        } finally {
                            javascriptPendingCalls--;
                        }
                    };
                }
                bindingOptions.MethodInterceptor = new LambdaMethodInterceptor(interceptorWrapper);
                chromium.JavascriptObjectRepository.Register(name, objectToBind, true, bindingOptions);
            }

            return true;
        }

        public T EvaluateScript<T>(string script) {
            return jsExecutor.EvaluateScript<T>(script, default(TimeSpan?));
        }

        public T EvaluateScript<T>(string script, TimeSpan? timeout) {
            return jsExecutor.EvaluateScript<T>(script, timeout);
        }

        public void ExecuteScript(string script) {
            jsExecutor.ExecuteScript(script);
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            jsExecutor.ExecuteScriptFunction(functionName, args);
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return jsExecutor.EvaluateScriptFunction<T>(functionName, args);
        }

        public bool CanGoBack {
            get { return chromium.CanGoBack; }
        }

        public bool CanGoForward {
            get { return chromium.CanGoForward; }
        }

        public void GoBack() {
            chromium.Back();
        }

        public void GoForward() {
            chromium.Forward();
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

        public Listener AttachListener(string name, Action handler, bool executeInUI = true) {
            Action<string> internalHandler = (eventName) => {
                if (!isDisposing && eventName == name) {
                    if (executeInUI) {
                        Dispatcher.Invoke(handler);
                    } else {
                        ExecuteWithAsyncErrorHandling(handler);
                    }
                }
            };
            var listener = new Listener(name, internalHandler);
            eventsListener.NotificationReceived += listener.Handler;
            return listener;
        }

        public void DetachListener(Listener listener) {
            eventsListener.NotificationReceived -= listener.Handler;
        }

        private void OnWebViewIsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (chromium.IsBrowserInitialized) {
                if (pendingInitialization != null) {
                    pendingInitialization();
                    pendingInitialization = null;
                }
                WebViewInitialized?.Invoke();
            }
        }

        private void OnWebViewFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            htmlToLoad = null;
            var navigated = Navigated;
            if (e.Frame.IsMain && navigated != null) {
                AsyncExecuteInUI(() => navigated.Invoke(e.Url));
            }
        }

        private void OnWebViewLoadError(object sender, LoadErrorEventArgs e) {
            htmlToLoad = null;
            var loadFailed = LoadFailed;
            if (e.ErrorCode != CefErrorCode.Aborted && loadFailed != null) {
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                AsyncExecuteInUI(() => loadFailed.Invoke(e.FailedUrl, (int) e.ErrorCode));
            }
        }

        private void OnWebViewTitleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TitleChanged?.Invoke();
        }

        public static void SetCookie(string url, string domain, string name, string value, DateTime expires) {
            var cookie = new Cookie() {
                Domain = domain,
                Name = name,
                Value = value,
                Expires = expires
            };
            Cef.GetGlobalCookieManager().SetCookieAsync(url, cookie);
        }

        public static string CookiesPath {
            set { Cef.GetGlobalCookieManager().SetStoragePath(value, true); }
        }

        private static bool FilterRequest(IRequest request) {
            return request.Url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase) ||
                   request.Url.Equals(DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase);
        }

        // TODO create a URL object to store this information and replace these methods
        public static string BuildEmbeddedResourceUrl(Assembly assembly, params string[] path) {
            return BuildEmbeddedResourceUrl(assembly.GetName().Name, path);
        }

        public static string BuildEmbeddedResourceUrl(string assemblyName, params string[] path) {
            return AssemblyPrefix + assemblyName + AssemblyPathSeparator + string.Join("/", path);
        }

        private static string GetEmbeddedResourceAssemblyName(Uri url) {
            if (url.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                var resourcePath = url.AbsoluteUri.Substring(AssemblyPrefix.Length);
                var indexOfPath = Math.Max(0, resourcePath.IndexOf(AssemblyPathSeparator));
                return resourcePath.Substring(0, indexOfPath);
            }
            return url.Segments.Length > 1 ? url.Segments[1].TrimEnd('/') : string.Empty; // default assembly name to the first path
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

        public event Action</*url*/string> PopupOpening {
            add { lifeSpanHandler.PopupOpening += value; }
            remove { lifeSpanHandler.PopupOpening -= value; }
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
                cancellationTokenSource.Token);
        }

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandling(Action action) {
            try {
                action();
            } catch (Exception e) {
                ForwardUnhandledAsyncException(e);
            }
        }

        private void ForwardUnhandledAsyncException(Exception e) {
            var handled = false;

            if (UnhandledAsyncException != null) {
                var eventArgs = new UnhandledExceptionEventArgs(e);
                UnhandledAsyncException?.Invoke(eventArgs);
                handled = eventArgs.Handled;
            }

            if (!handled) {
                // don't use invoke async, as it won't forward the exception to the dispatcher unhandled exception event
                Dispatcher.BeginInvoke((Action)(() => throw e));
            }
        }
    }
}
