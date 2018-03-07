using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace WebViewControl {

    public partial class ReactView : ContentControl, IDisposable {

        private const string PathSeparator = "/";
        private const string RootObject = "__Root__";
        private const string ReadyEventName = "Ready";

        private static readonly string AssemblyName = typeof(ReactView).Assembly.GetName().Name;
        private static readonly string BuiltinResourcesPath = AssemblyName + "/Resources/";
        private static readonly string DefaultUrl = BuiltinResourcesPath + "index.html";

        private readonly WebView webView = new WebView();
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private bool isSourceSet = false;
        private Listener readyEventListener;

        public ReactView() {
            Initialize(CreateRootPropertiesObject());
        }

        public ReactView(object rootProperties) {
            Initialize(rootProperties);
        }

        public static bool UseEnhancedRenderingEngine { get; set; } = true;

        private void Initialize(object rootProperties) {
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited

            userCallingAssembly = WebView.GetUserCallingAssembly();

            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener(ReadyEventName, () => IsReady = true, executeInUI: false);

            if (rootProperties != null) {
                webView.RegisterJavascriptObject("__RootProperties__", rootProperties, executeCallsInUI: false);
            }

            Content = webView;
        }

        public event Action Ready {
            add { readyEventListener = webView.AttachListener(ReadyEventName, value); }
            remove {
                if (readyEventListener != null) {
                    webView.DetachListener(readyEventListener);
                }
            }
        }

        public event Action<UnhandledExceptionEventArgs> UnhandledAsyncException {
            add { webView.UnhandledAsyncException += value; }
            remove { webView.UnhandledAsyncException -= value; }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            if (!isSourceSet) {
                // wait for default stylesheet property to be set
                LoadSource();
                isSourceSet = true;
            }
        }

        private void LoadSource() {
            const string JsExtension = ".js";

            var source = NormalizeUrl(Source ?? "");
            if (source.EndsWith(JsExtension)) {
                source = source.Substring(0, source.Length - JsExtension.Length);
            }

            var filenameParts = source.Split(new[] { PathSeparator }, StringSplitOptions.None);

            // eg: example/dist/source.js
            // defaultSource = ./dist/source.js
            // baseUrl = /AssemblyName/example/
            var sourceDepth = filenameParts.Length >= 2 ? 2 : 1; 
            var defaultSource = "./" + string.Join(PathSeparator, filenameParts.Reverse().Take(sourceDepth).Reverse()); // take last 2 parts of the path
            var baseUrl = ToFullUrl(string.Join(PathSeparator, filenameParts.Take(filenameParts.Length - sourceDepth))) + PathSeparator;

            var urlParams = new string[] {
                PathSeparator + BuiltinResourcesPath,
                baseUrl,
                defaultSource,
                "",
                "",
                UseEnhancedRenderingEngine ? "1" : "0",
            };

            if (AdditionalModule != null) {
                urlParams[3] = NormalizeUrl(ToFullUrl(AdditionalModule));
            }

            if (DefaultStyleSheet != null) {
                urlParams[4] = NormalizeUrl(ToFullUrl(DefaultStyleSheet));
            }

            var url = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultUrl + "?" + string.Join("&", urlParams));
            webView.Address = url;
        }

        private static string NormalizeUrl(string url) {
            return url.Replace("\\", PathSeparator);
        }

        private string ToFullUrl(string url) {
            return (url.StartsWith(PathSeparator) || url.Contains(Uri.SchemeDelimiter)) ? url : $"/{userCallingAssembly.GetName().Name}/{url}";
        }

        public void Dispose() {
            webView.Dispose();
        }

        protected void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            webView.ExecuteScriptFunction(RootObject + "." + methodCall, args);
        }

        protected T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return webView.EvaluateScriptFunction<T>(RootObject + "." + methodCall, args);
        }

        protected virtual object CreateRootPropertiesObject() {
            return null;
        }

        protected virtual string Source => null;

        public static readonly DependencyProperty DefaultStyleSheetProperty = DependencyProperty.Register(
            nameof(DefaultStyleSheet), 
            typeof(string),
            typeof(ReactView));

        public string DefaultStyleSheet {
            get { return (string) GetValue(DefaultStyleSheetProperty); }
            set { SetValue(DefaultStyleSheetProperty, value); }
        }

        public static readonly DependencyProperty AdditionalModuleProperty = DependencyProperty.Register(
            nameof(AdditionalModule),
            typeof(string),
            typeof(ReactView));

        public string AdditionalModule {
            get { return (string)GetValue(AdditionalModuleProperty); }
            set { SetValue(AdditionalModuleProperty, value); }
        }

        public bool IsReady { get; private set; }
        
        public bool EnableDebugMode {
            get { return enableDebugMode; }
            set {
                enableDebugMode = value;
                webView.AllowDeveloperTools = enableDebugMode;
                if (enableDebugMode) {
                    webView.ResourceLoadFailed += ShowResourceLoadFailedMessage;
                } else {
                    webView.ResourceLoadFailed -= ShowResourceLoadFailedMessage;
                }
            }
        }

        private void ShowResourceLoadFailedMessage(string url) {
            ShowErrorMessage("Failed to load resource '" + url + "'. Press F12 to open developer tools and see more details.");
        }

        private void ShowErrorMessage(string msg) {
            msg = msg.Replace("\"", "\\\"");
            webView.ExecuteScript($"showErrorMessage(\"{msg}\")");
        }
    }
}
