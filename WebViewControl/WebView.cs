using CefSharp;
using CefSharp.ModelBinding;
using CefSharp.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WebViewControl {

    public partial class WebView : UserControl, IDisposable {

        private static readonly string[] CustomSchemes = new[] {
            ResourceUrl.LocalScheme,
            ResourceUrl.EmbeddedScheme,
            ResourceUrl.CustomScheme
        };

        private const string ChromeInternalProtocol = "chrome-devtools:";

        private static bool subscribedApplicationExit = false;

        private readonly object SyncRoot = new object();
        private InternalChromiumBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private Action pendingInitialization;
        private string htmlToLoad;
        private JavascriptExecutor jsExecutor;
        private CefLifeSpanHandler lifeSpanHandler;
        private CefResourceHandlerFactory resourceHandlerFactory;
        private volatile bool isDisposing;
        private volatile int javascriptPendingCalls;

        private readonly DefaultBinder binder = new DefaultBinder(new DefaultFieldNameConverter());
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
        public event Action<UnhandledAsyncExceptionEventArgs> UnhandledAsyncException;
        internal event Action Disposed;

        private event Action RenderProcessCrashed;
        private event Action JavascriptContextReleased;
        private event Action JavascriptCallFinished;

        private readonly string DefaultLocalUrl;

        /// <summary>
        /// Executed when a web view is initialized. Can be used to attach or configure the webview before it's ready.
        /// </summary>
        public static event Action<WebView> GlobalWebViewInitialized;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeCef() {
            if (!Cef.IsInitialized) {
                var cefSettings = new CefSettings();
                cefSettings.LogSeverity = string.IsNullOrWhiteSpace(LogFile) ? LogSeverity.Disable : (EnableErrorLogOnly ? LogSeverity.Error : LogSeverity.Verbose);
                cefSettings.LogFile = LogFile;
                cefSettings.UncaughtExceptionStackSize = 100; // enable stack capture
                cefSettings.CachePath = CachePath; // enable cache for external resources to speedup loading
                cefSettings.WindowlessRenderingEnabled = true;

                if (DisableGPU) {
                    cefSettings.CefCommandLineArgs.Add("disable-gpu", "1"); // Disable GPU acceleration
                    cefSettings.CefCommandLineArgs.Add("disable-gpu-vsync", "1"); //Disable GPU vsync
                }

                CefSharpSettings.ConcurrentTaskExecution = true;
                CefSharpSettings.LegacyJavascriptBindingEnabled = true;
                CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

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

            DefaultLocalUrl = new ResourceUrl(ResourceUrl.LocalScheme, "index.html").ToString();

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

            lifeSpanHandler = new CefLifeSpanHandler(this);
            resourceHandlerFactory = new CefResourceHandlerFactory(this);

            chromium = new InternalChromiumBrowser();
            chromium.IsBrowserInitializedChanged += OnWebViewIsBrowserInitializedChanged;
            chromium.FrameLoadEnd += OnWebViewFrameLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += OnWebViewTitleChanged;
            chromium.PreviewKeyDown += OnPreviewKeyDown;
            chromium.RequestHandler = new CefRequestHandler(this);
            chromium.ResourceHandlerFactory = resourceHandlerFactory;
            chromium.LifeSpanHandler = lifeSpanHandler;
            chromium.RenderProcessMessageHandler = new CefRenderProcessMessageHandler(this);
            chromium.MenuHandler = new CefMenuHandler(this);
            chromium.DialogHandler = new CefDialogHandler(this);
            chromium.DownloadHandler = new CefDownloadHandler(this);
            chromium.CleanupElement = new FrameworkElement(); // prevent chromium to listen to default cleanup element unload events, this will be controlled manually

            jsExecutor = new JavascriptExecutor(this);

            RegisterJavascriptObject(Listener.EventListenerObjName, eventsListener);

            Content = chromium;

            GlobalWebViewInitialized?.Invoke(this);

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

                cancellationTokenSource.Cancel();

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

                resourceHandlerFactory.Dispose();

                jsExecutor.Dispose();
                chromium.Dispose();
                cancellationTokenSource.Dispose();

                Disposed?.Invoke();
            }

            // avoid dead-lock, wait for all pending calls to finish
            JavascriptCallFinished += () => {
                if (javascriptPendingCalls == 0) {
                    Dispatcher.BeginInvoke((Action) InternalDispose);
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
            set { Load(value); }
        }

        private void Load(string address) {
            if (address != DefaultLocalUrl) {
                htmlToLoad = null;
            }
            if (address.Contains(Uri.SchemeDelimiter) || address == "about:blank" || address.StartsWith("data:")) {
                var settings = chromium.BrowserSettings;
                if (settings != null /* TODO uncomment after upgrade to 71+ && !settings.IsDisposed*/) {
                    if (CustomSchemes.Any(s => address.StartsWith(s + Uri.SchemeDelimiter))) {
                        // custom schemes -> turn off security ... to enable full access without problems to local resources
                        IsSecurityDisabled = true;
                    } else {
                        IsSecurityDisabled = false;
                    }
                }
                // must wait for the browser to be initialized otherwise navigation will be aborted
                ExecuteWhenInitialized(() => chromium.Load(address));
            } else {
                var userAssembly = GetUserCallingMethod().ReflectedType.Assembly;
                Load(new ResourceUrl(userAssembly, address).ToString());
            }
        }

        public void LoadResource(ResourceUrl resourceUrl) {
            Address = resourceUrl.ToString();
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            Load(DefaultLocalUrl);
        }

        public bool IsSecurityDisabled {
            set {
                var settings = chromium.BrowserSettings;
                if (settings == null /* TODO uncomment after upgrade to 71+ || settings.IsDisposed*/) {
                    throw new InvalidOperationException("Cannot change webview settings after initialized");
                }
                settings.WebSecurity = (value ? CefState.Disabled : CefState.Enabled);
            }
        }

        public bool IgnoreCertificateErrors { get; set; }

        public bool IsHistoryDisabled { get; set; }

        public TimeSpan? DefaultScriptsExecutionTimeout { get; set; }

        public bool DisableBuiltinContextMenus { get; set; }

        public bool DisableFileDialogs { get; set; }

        public bool IsBrowserInitialized {
            get { return chromium.IsBrowserInitialized; }
        }

        public bool IsJavascriptEngineInitialized {
            get { return chromium.CanExecuteJavascriptInMainFrame; }
        }

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

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
                return RegisterJavascriptObject(name, objectToBind, target => Dispatcher.Invoke(target), bind, false);

            } else {
                var bindingOptions = new BindingOptions();
                if (bind != null) {
                    bindingOptions.Binder = new LambdaMethodBinder(bind);
                } else {
                    bindingOptions.Binder = binder;
                }

                if (interceptCall == null) {
                    interceptCall = target => target();
                }

                object WrapCall(Func<object> target) {
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

                bindingOptions.MethodInterceptor = new LambdaMethodInterceptor(WrapCall);
                chromium.JavascriptObjectRepository.Register(name, objectToBind, true, bindingOptions);
            }

            return true;
        }

        public T EvaluateScript<T>(string script) {
            return jsExecutor.EvaluateScript<T>(script, timeout: default(TimeSpan?));
        }

        public T EvaluateScript<T>(string script, TimeSpan? timeout) {
            return jsExecutor.EvaluateScript<T>(script, timeout: timeout);
        }

        public void ExecuteScript(string script) {
            jsExecutor.ExecuteScript(script);
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            jsExecutor.ExecuteScriptFunction(functionName, false, args);
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return jsExecutor.EvaluateScriptFunction<T>(functionName, false, args);
        }

        internal void ExecuteScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            jsExecutor.ExecuteScriptFunction(functionName, true, args);
        }

        internal T EvaluateScriptFunctionWithSerializedParams<T>(string functionName, params object[] args) {
            return jsExecutor.EvaluateScriptFunction<T>(functionName, true, args);
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
            get {
                double.TryParse(EvaluateScript<string>("document.body.style.zoom"), NumberStyles.Number, CultureInfo.InvariantCulture, out var zoom);
                return zoom;
            }
            set { ExecuteScript("document.body.style.zoom = " + value.ToString(CultureInfo.InvariantCulture)); }
        }

        public Listener AttachListener(string name) {
            void HandleEvent(Action handler, bool executeInUI) {
                if (!isDisposing) {
                    if (executeInUI) {
                        Dispatcher.Invoke(handler);
                    } else {
                        ExecuteWithAsyncErrorHandling(handler);
                    }
                }
            }

            return new Listener(name, HandleEvent, eventsListener);
        }

        private void OnWebViewIsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e) {
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

        private bool FilterRequest(IRequest request) {
            return request.Url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase) ||
                   request.Url.Equals(DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase);
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
            if (isDisposing) {
                return;
            }

            var handled = false;
            
            var unhandledAsyncException = UnhandledAsyncException;
            if (unhandledAsyncException != null) {
                var eventArgs = new UnhandledAsyncExceptionEventArgs(e);
                unhandledAsyncException(eventArgs);
                handled = eventArgs.Handled;
            }

            if (!handled) {
                // don't use invoke async, as it won't forward the exception to the dispatcher unhandled exception event
                Dispatcher.BeginInvoke((Action) (() => {
                    if (!isDisposing) {
                        throw e;
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

        public static bool DisableGPU { get; set; } = false;

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
            Dispose();
        }

        protected void RegisterProtocolHandler(string protocol, CefResourceHandlerFactory handler) {
            if (chromium.RequestContext == null) {
                // create a new context to turn off zoom sharing between same domains
                chromium.RequestContext = new RequestContext(new RequestContextSettings() {
                    CachePath = CachePath
                });
            }
            chromium.RequestContext.RegisterSchemeHandlerFactory(protocol, "", handler);
        }
        
        protected virtual string GetRequestUrl(string url, ResourceType resourceType) {
            return url;
        }
    }
}
