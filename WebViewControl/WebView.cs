using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private const string ChromeInternalProtocol = "chrome-devtools:";
        
        protected const string DefaultPath = "://webview/";
        private const string DefaultLocalUrl = LocalScheme + DefaultPath + "index.html";
        private const string AssemblyPrefix = EmbeddedScheme + DefaultPath + "assembly:";
        private const string AssemblyPathSeparator = ";";

        // converts cef zoom percentage to css zoom (between 0 and 1)
        // from https://code.google.com/p/chromium/issues/detail?id=71484
        private const float PercentageToZoomFactor = 1.2f;

        private readonly DefaultBinder binder = new DefaultBinder(new DefaultFieldNameConverter());

        protected InternalChromiumBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private BrowserSettings settings;
        private bool isDisposing;
        private Action pendingInitialization;
        private string htmlToLoad;
        private JavascriptExecutor jsExecutor;

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
        public event Action<string> DownloadCanceled;
        public event Action JavascriptContextCreated;
        public event Action TitleChanged;

        private event Action RenderProcessCrashed;

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
                var tempDir = Path.GetTempPath();// Storage.NewStorageWithFilename("WebView");
                var cefSettings = new CefSettings();
                cefSettings.LogSeverity = LogSeverity.Disable; // disable writing of debug.log
                
                // TODO jmn not needed probably cefSettings.CachePath = tempDir; // enable cache for external resources to speedup loading

                foreach (var scheme in CustomSchemes) {
                    cefSettings.RegisterScheme(new CefCustomScheme() {
                        SchemeName = scheme,
                        SchemeHandlerFactory = new CefSchemeHandlerFactory()
                    });
                }
                // as we cannot obtain the default value of the user agent used in CEF we are hardcoding the first part of the string
                //cefSettings.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.22 (KHTML, like Gecko) Safari/537.22 Chrome/" + Cef.ChromiumVersion + " DevelopmentEnvironment/" + OmlConstants.Version;
                cefSettings.BrowserSubprocessPath = CefLoader.GetBrowserSubProcessPath();

                Cef.Initialize(cefSettings, performDependencyCheck: false, browserProcessHandler: null);

                Application.Current.Exit += (o, e) => Cef.Shutdown(); // must shutdown cef to free cache files (so that storage cleanup on process exit is able to delete files)
            }
        }

        public WebView() {
            if (DesignerProperties.GetIsInDesignMode(this)) {
                return;
            }

            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize() {
            settings = new BrowserSettings();
            settings.JavascriptOpenWindows = CefState.Disabled;

            chromium = new InternalChromiumBrowser();
            chromium.BrowserSettings = settings;
            chromium.IsBrowserInitializedChanged += OnWebViewIsBrowserInitializedChanged;
            chromium.FrameLoadEnd += OnWebViewFrameLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.TitleChanged += OnWebViewTitleChanged;
            chromium.PreviewKeyDown += OnPreviewKeyDown;
            chromium.RequestHandler = new CefRequestHandler(this);
            chromium.ResourceHandlerFactory = new CefResourceHandlerFactory(this);
            chromium.LifeSpanHandler = new CefLifeSpanHandler(this);
            chromium.RenderProcessMessageHandler = new RenderProcessMessageHandler(this);
            chromium.MenuHandler = new MenuHandler(this);
            chromium.DialogHandler = new CefDialogHandler(this);
            // TODO chromium.DownloadHandler = new CefDownloadHandler();

            jsExecutor = new JavascriptExecutor(this);
            Content = chromium;

            var initialized = GlobalWebViewInitialized;
            if (initialized != null) {
                initialized(this);
            }
        }

        public void Dispose() {
            isDisposing = true;

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
            settings = null;
            chromium = null;
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
                if (value.Contains("://") || value == "about:blank") {
                    // must wait for the browser to be initialized otherwise navigation will be aborted
                    ExecuteWhenInitialized(() => chromium.Load(value));
                } else {
                    LoadFrom(value);
                }
            }
        }

        public bool IsSecurityDisabled {
            get { return settings.WebSecurity != CefState.Enabled; }
            set { settings.WebSecurity = value ? CefState.Disabled : CefState.Enabled; }
        }

        public bool IsHistoryDisabled {
            get { return false/* TODO JMN cef3 settings.HistoryDisabled*/; }
            set { /*settings.HistoryDisabled = value;*/ }
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

        public virtual void RegisterJavascriptObject(string name, object objectToBind) {
            chromium.RegisterAsyncJsObject(name, objectToBind, new BindingOptions() { Binder = binder });
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

        private void OnWebViewIsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (chromium.IsBrowserInitialized) {
                if (pendingInitialization != null) {
                    pendingInitialization();
                    pendingInitialization = null;
                }
                if (WebViewInitialized != null) {
                    WebViewInitialized();
                }
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
            if (LoadFailed != null) {
                ExecuteInUIThread(() => LoadFailed(e.FailedUrl, (int) e.ErrorCode));
            }
        }

        private void OnWebViewTitleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (TitleChanged != null) {
                TitleChanged();
            }
        }

        private void ExecuteInUIThread(Action action) {
            // use begin invoke to avoid dead-locks, otherwise if another any browser instance tries, for instance, to evaluate js it would block
            Dispatcher.BeginInvoke(
                (Action) (() => {
                    if (chromium != null) {
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
                var indexOfPath = resourcePath.IndexOf(AssemblyPathSeparator);
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
            return name == "PresentationFramework" || name == "mscorlib" || name == "System.Xaml";
        }

        protected static Assembly GetUserCallingAssembly() {
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
    }
}
