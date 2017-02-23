using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;

namespace WebViewControl {

    public partial class WebView : ContentControl, IDisposable {

        private const string LocalScheme = "local";
        private const string EmbeddedScheme = "embedded";
        private static readonly string[] CustomSchemes = new[] {
            LocalScheme,
            EmbeddedScheme
        };

        protected const string BuiltinResourcesPath = "builtin/";
        private const string DefaultPath = "://webview/";
        private const string DefaultLocalUrl = LocalScheme + DefaultPath + "index.html";
        protected const string DefaultEmbeddedUrl = EmbeddedScheme + DefaultPath + "index.html";

        private CefSharp.Wpf.ChromiumWebBrowser chromium;
        private bool isDeveloperToolsOpened = false;
        private BrowserSettings settings;
        private bool isDisposing;
        private bool firstLoadCompleted;
        private Action pendingInitialization;
        private Action scrollChanged;
        private string htmlToLoad;
        private JavascriptContextProvider jsContextProvider;
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
        public event Action<string> ResourceLoadFailed;
        public event Action<string, long, long> DownloadProgressChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string> DownloadCanceled;
        public event Action JavascriptContextCreated;

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

                // TODO JMN cef 3 probably not needed cefSettings.RegisterScheme(new CefCustomScheme() {
                //    SchemeName = LocalScheme,
                //});

                Cef.Initialize(cefSettings);

                Application.Current.Exit += (o, e) => Cef.Shutdown(); // must shutdown cef to free cache files (so that storage cleanup on process exit is able to delete files)
            }
        }

        public WebView() {
            settings = new BrowserSettings();
            settings.JavascriptOpenWindows = CefState.Disabled;
            
            chromium = new CefSharp.Wpf.ChromiumWebBrowser();
            chromium.BrowserSettings = settings;
            chromium.IsBrowserInitializedChanged += OnWebViewIsBrowserInitializedChanged;
            chromium.FrameLoadEnd += OnWebViewFrameLoadEnd;
            chromium.LoadError += OnWebViewLoadError;
            chromium.RequestHandler = new CefRequestHandler(this);
            chromium.ResourceHandlerFactory = new CefResourceHandlerFactory(this);
            chromium.LifeSpanHandler = new CefLifeSpanHandler(this);
            chromium.PreviewKeyDown += OnPreviewKeyDown;
            chromium.RenderProcessMessageHandler = new RenderProcessMessageHandler(this);
            jsContextProvider = new JavascriptContextProvider(this);
            Content = chromium; 
        }

        public void Dispose() {
            isDisposing = true;

            // TODO JMN cef3
            // webView.JavascriptContextCreated -= OnJavascriptContextCreated;
            chromium.RequestHandler = null;
            chromium.ResourceHandlerFactory = null;
            chromium.PreviewKeyDown -= OnPreviewKeyDown;
            BrowserInitialized = null;
            BeforeNavigate = null;
            BeforeResourceLoad = null;
            Navigated = null;
            ResourceLoadFailed = null;
            scrollChanged = null;
            
            CloseDeveloperTools();
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
            chromium.ShowDevTools();
            isDeveloperToolsOpened = true;
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

        public bool IsBrowserInitialized {
            get { return chromium.IsBrowserInitialized; }
        }

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public void Load(string url) {
            Action initialize = () => chromium.Load(url);
            if (chromium.IsBrowserInitialized) {
                initialize();
            } else {
                pendingInitialization = initialize;
            }
        }

        public void LoadFrom(string resourcesNamespace) {
            LoadFrom(Assembly.GetExecutingAssembly(), resourcesNamespace);
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

        public void RegisterJavascriptObject(string name, object objectToBind) {
            chromium.RegisterJsObject(name, objectToBind);
        }

        private object InternalEvaluateScript(string script, TimeSpan? timeout = default(TimeSpan?)) {
            var task = chromium.EvaluateScriptAsync(script, timeout);
            task.Wait();
            if (task.Result.Success) {
                return task.Result.Result;
            }
            throw new JavascriptException(task.Result.Message);   
        }

        private void InternalExecuteScript(string script) {
            chromium.ExecuteScriptAsync(script);
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            jsContextProvider.ExecuteScriptFunction(functionName, args);
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return jsContextProvider.EvaluateScriptFunction<T>(functionName, args);
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
            // TODO JMN cef3 load error?
            if (ResourceLoadFailed != null) {
                ExecuteInUIThread(() => ResourceLoadFailed(e.FailedUrl));
            }
        }

        private void OnJavascriptContextCreated(object sender) {
            // TODO
            if (JavascriptContextCreated != null) {
                JavascriptContextCreated();
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

        private bool FilterRequest(IRequest request) {
            return request.Url.ToLower().StartsWith("chrome-") ||
                   request.Url.Equals(DefaultLocalUrl, StringComparison.InvariantCultureIgnoreCase) ||
                   request.Url.Equals(DefaultEmbeddedUrl, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
