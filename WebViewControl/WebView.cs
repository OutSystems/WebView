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
        protected const string EmbeddedScheme = "embedded";
        private static readonly string[] CustomSchemes = new[] {
            LocalScheme,
            EmbeddedScheme
        };

        private static readonly string TempDir = 
            Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.Now.Ticks);

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
        private BrowserObjectListener eventsListener = new BrowserObjectListener();
        private CefLifeSpanHandler lifeSpanHandler;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

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
        public event Action<string, long, long> DownloadProgressChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string> DownloadCancelled;
        public event Action JavascriptContextCreated;
        public event Action TitleChanged;

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

                cefSettings.CachePath = TempDir; // enable cache for external resources to speedup loading

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
            chromium.RenderProcessMessageHandler = new RenderProcessMessageHandler(this);
            chromium.MenuHandler = new MenuHandler(this);
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

            cancellationTokenSource.Cancel();
            chromium.RequestHandler = null;
            chromium.ResourceHandlerFactory = null;
            chromium.PreviewKeyDown -= OnPreviewKeyDown;
            WebViewInitialized = null;
            BeforeNavigate = null;
            BeforeResourceLoad = null;
            Navigated = null;
            LoadFailed = null;

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
            ExecuteWhenInitialized(() => {
                chromium.Dispatcher.BeginInvoke((Action) (() => chromium.Focus()));
            });
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
            var userAssembly = GetUserCallingAssembly();

            IsSecurityDisabled = true;
            Address = BuildEmbeddedResourceUrl(userAssembly, userAssembly.GetName().Name, source);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            Address = DefaultLocalUrl;
        }

        public void RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, CancellationToken, object> interceptCall = null, Func<object, Type, object> bind = null) {
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
                    return interceptCall(target, cancellationTokenSource.Token);
                };
            } else {
                interceptorWrapper = target => {
                    if (isDisposing) {
                        return null;
                    }
                    return target();
                };
            }
            bindingOptions.MethodInterceptor = new LambdaMethodInterceptor(interceptorWrapper);
            chromium.RegisterAsyncJsObject(name, objectToBind, bindingOptions);
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

        public string Title {
            get { return chromium.Title; }
        }

        public double ZoomPercentage {
            get { return Math.Pow(PercentageToZoomFactor, chromium.ZoomLevel); }
            set {
                ExecuteWhenInitialized(() => chromium.ZoomLevel = Math.Log(value, PercentageToZoomFactor));
            }
        }

        public Listener AttachListener(string name, Action handler, bool executeInUIThread = true) {
            Action<string> internalHandler = (eventName) => {
                if (!isDisposing && eventName == name) {
                    if (executeInUIThread) {
                        // invoke async otherwise if we try to execute some script  on the browser as a result of this notification, it will block forever
                        Application.Current.Dispatcher.InvokeAsync(handler, DispatcherPriority.Normal, cancellationTokenSource.Token);
                    } else {
                        handler();
                    }
                }
            };
            var listener = new Listener(name, internalHandler);
            eventsListener.NotificationReceived += internalHandler;
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
            if (e.Frame.IsMain) {
                if (Navigated != null) {
                    ExecuteInUIThread(() => Navigated(e.Url));
                }
            }
        }

        private void OnWebViewLoadError(object sender, LoadErrorEventArgs e) {
            htmlToLoad = null;
            if (e.ErrorCode != CefErrorCode.Aborted && LoadFailed != null) {
                // ignore aborts, to prevent situations where we try to load an address inside Load failed handler (and its aborted)
                ExecuteInUIThread(() => LoadFailed(e.FailedUrl, (int) e.ErrorCode));
            }
        }

        private void OnWebViewTitleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TitleChanged?.Invoke();
        }
        
        private void ExecuteInUIThread(Action action) {
            if (isDisposing) {
                return;
            }
            // use begin invoke to avoid dead-locks, otherwise if another any browser instance tries, for instance, to evaluate js it would block
            Dispatcher.BeginInvoke(
                (Action) (() => {
                    if (!isDisposing) {
                        // not disposed
                        action();
                    }
                }));
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
            return request.Url.ToLower().StartsWith(ChromeInternalProtocol) ||
                   request.Url.Equals(DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase);
        }

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

        private static string GetEmbeddedResourcePath(Uri url) {
            if (url.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                var indexOfPath = url.AbsolutePath.IndexOf(AssemblyPathSeparator);
                return url.AbsolutePath.Substring(indexOfPath + 1);
            }
            return string.Empty;
        }

        private static bool IsFrameworkAssemblyName(string name) {
            return name == "PresentationFramework" || name == "PresentationCore" || name == "mscorlib" || name == "System.Xaml";
        }

        internal static Assembly GetUserCallingAssembly() {
            var currentAssembly = typeof(WebView).Assembly;
            var callingAssemblies = new StackTrace().GetFrames().Select(f => f.GetMethod().ReflectedType.Assembly).Where(a => a != currentAssembly);
            var userAssembly = callingAssemblies.First(a => !IsFrameworkAssemblyName(a.GetName().Name));
            if (userAssembly == null) {
                throw new InvalidOperationException("Unable to find calling assembly");
            }
            return userAssembly;
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

        [DebuggerNonUserCode]
        private void WithErrorHandling(Action action) {
            try {
                action();
            } catch (Exception e) {
                ExecuteInUIThread(() => { throw e; });
            }
        }
    }
}
