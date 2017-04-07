using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
        private const string EmbeddedScheme = "embedded";
        private static readonly string[] CustomSchemes = new[] {
            LocalScheme,
            EmbeddedScheme
        };

        private const string ChromeInternalProtocol = "chrome-devtools:";

        protected const string BuiltinResourcesPath = "builtin/";
        private const string DefaultPath = "://webview/";
        private const string DefaultLocalUrl = LocalScheme + DefaultPath + "index.html";
        protected const string DefaultEmbeddedUrl = EmbeddedScheme + DefaultPath + "index.html";

        protected CefSharp.Wpf.ChromiumWebBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private BrowserSettings settings;
        private bool isDisposing;
        private bool firstLoadCompleted;
        private Action pendingInitialization;
        private Action scrollChanged;
        private string htmlToLoad;
        private JavascriptExecutor jsExecutor;
        private Assembly resourcesSource;
        private string resourcesNamespace;

        public event Action BrowserInitialized;

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

        static WebView() {
            if (!Cef.IsInitialized) {
                var tempDir = Path.GetTempPath();// Storage.NewStorageWithFilename("WebView");
                var cefSettings = new CefSettings();
                cefSettings.LogSeverity = LogSeverity.Verbose; // disable writing of debug.log
                
                // TODO jmn not needed probably cefSettings.CachePath = tempDir; // enable cache for external resources to speedup loading

                foreach (var scheme in CustomSchemes) {
                    cefSettings.RegisterScheme(new CefCustomScheme() {
                        SchemeName = scheme,
                        SchemeHandlerFactory = new CefSchemeHandlerFactory()
                    });
                }
                // as we cannot obtain the default value of the user agent used in CEF we are hardcoding the first part of the string
                //cefSettings.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.22 (KHTML, like Gecko) Safari/537.22 Chrome/" + Cef.ChromiumVersion + " DevelopmentEnvironment/" + OmlConstants.Version;
                //cefSettings.BrowserSubprocessPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "CefSharp.BrowserSubprocess.exe");

                Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: null);

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

            chromium = new CefSharp.Wpf.ChromiumWebBrowser();
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
            // TODO chromium.DownloadHandler = new CefDownloadHandler();
            
            jsExecutor = new JavascriptExecutor(this);
            Content = chromium;
        }

        public void Dispose() {
            isDisposing = true;

            chromium.RequestHandler = null;
            chromium.ResourceHandlerFactory = null;
            chromium.PreviewKeyDown -= OnPreviewKeyDown;
            BrowserInitialized = null;
            BeforeNavigate = null;
            BeforeResourceLoad = null;
            Navigated = null;
            LoadFailed = null;
            scrollChanged = null;
            
            CloseDeveloperTools();
            jsExecutor.Dispose();
            settings.Dispose();
            chromium.Dispose();
            settings = null;
            chromium = null;
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
            if (IsBrowserInitialized) {
                chromium.ShowDevTools();
                isDeveloperToolsOpened = true;
            } else {
                pendingInitialization += ShowDeveloperTools;
            }
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
            set { chromium.Address = value; }
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

        public bool IsBrowserInitialized {
            get { return chromium.IsBrowserInitialized; }
        }

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public void Load(string url) {
            Action initialize = () => chromium.Load(url);
            if (chromium.IsBrowserInitialized) {
                initialize();
            } else {
                pendingInitialization += initialize;
            }
        }

        public void LoadFrom(string resourcesNamespace) {
            LoadFrom(Assembly.GetCallingAssembly(), resourcesNamespace);
        }

        protected void LoadFrom(Assembly assembly, string resourcesNamespace) {
            resourcesSource = assembly;
            this.resourcesNamespace = resourcesNamespace;
            IsSecurityDisabled = true;
            Load(DefaultEmbeddedUrl);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            Load(DefaultLocalUrl);
        }

        public virtual void RegisterJavascriptObject(string name, object objectToBind) {
            chromium.RegisterAsyncJsObject(name, objectToBind);
        }

        public T EvaluateScript<T>(string script, TimeSpan? timeout = default(TimeSpan?)) {
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

        public double ZoomLevel {
            get { return chromium.ZoomLevel; }
            set { chromium.ZoomLevel = value; }
        }

        private void OnWebViewIsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (chromium.IsBrowserInitialized) {
                if (pendingInitialization != null) {
                    pendingInitialization();
                    pendingInitialization = null;
                }
                if (BrowserInitialized != null) {
                    BrowserInitialized();
                }
            }
        }

        private void OnWebViewFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            htmlToLoad = null;
            if (e.Frame.IsMain) {
                firstLoadCompleted = true;
                if (Navigated != null) {
                    if (scrollListenerRegistered) {
                        RegisterScrollChangesOnPage();
                    }
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

        // TODO scoll
        public event Action ScrollChanged {
            add {
                scrollChanged += value;
                if (!scrollListenerRegistered) {
                    RegisterScrollListener();
                }
                if (firstLoadCompleted) {
                    RegisterScrollChangesOnPage();
                }
            }
            remove {
                scrollChanged -= value;
            }
        }

        private const string ScrollListenerObj = "__ssScrollChanged";
        private bool scrollListenerRegistered;
        private void RegisterScrollListener() {    
            var scrollListener = new Listener();
            scrollListener.NotificationReceived = scrollChanged;
            RegisterJavascriptObject(ScrollListenerObj, scrollListener);
            scrollListenerRegistered = true;
        }

        private void RegisterScrollChangesOnPage() {
            // TODO ExecuteScript("window.onscroll = function () { " + ScrollListenerObj + ".notify(); }");
        }

        private static bool FilterRequest(IRequest request) {
            return request.Url.ToLower().StartsWith(ChromeInternalProtocol) ||
                   request.Url.Equals(DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase) ||
                   request.Url.Equals(DefaultEmbeddedUrl, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
