using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace WebViewControl {

    internal partial class ReactViewRender : ContentControl, IReactView {

        private const string PathSeparator = "/";
        private const string RootObject = "__Root__";
        private const string RootPropertiesName = "__RootProperties__";
        private const string ReadyEventName = "Ready";

        private static readonly string AssemblyName = typeof(ReactViewRender).Assembly.GetName().Name;
        private static readonly string BuiltinResourcesPath = $"{AssemblyName}/Resources/";
        private static readonly string DefaultUrl = $"{BuiltinResourcesPath}index.html";
        private static readonly string LibrariesPath = $"/{BuiltinResourcesPath}node_modules/";

        private readonly WebView webView = new WebView();
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private Listener readyEventListener;
        private string source;
        private object rootProperties;
        private bool pageLoaded = false;

        public static bool UseEnhancedRenderingEngine { get; set; } = true;

        public ReactViewRender() {
            userCallingAssembly = WebView.GetUserCallingAssembly();

            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener(ReadyEventName, () => IsReady = true, executeInUI: false);
            webView.Navigated += OnWebViewNavigated;

            Content = webView;
            LoadFramework();
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

        private void LoadFramework() {
            var urlParams = new string[] {
                UseEnhancedRenderingEngine ? "1" : "0",
                LibrariesPath,
                RootObject,
                RootPropertiesName,
                Listener.EventListenerObjName,
                ReadyEventName
            };

            webView.Address = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultUrl + "?" + string.Join("&", urlParams));
        }

        public void LoadComponent(string source, object rootProperties) {
            this.source = source;
            this.rootProperties = rootProperties;
            if (pageLoaded) {
                InternalLoadComponent();
            }
        }

        private void InternalLoadComponent() {
            const string JsExtension = ".js";

            source = NormalizeUrl(source ?? "");
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
            var additionalModule = "";
            var defaultStyleSheet = "";

            if (AdditionalModule != null) {
                additionalModule = NormalizeUrl(ToFullUrl(AdditionalModule));
            }

            if (DefaultStyleSheet != null) {
                defaultStyleSheet = NormalizeUrl(ToFullUrl(DefaultStyleSheet));
            }

            webView.RegisterJavascriptObject(RootPropertiesName, rootProperties ?? new object(), executeCallsInUI: false);

            webView.ExecuteScriptFunction("load", Quote(baseUrl), Quote(defaultSource), Quote(additionalModule), Quote(defaultStyleSheet));
        }

        private void OnWebViewNavigated(string obj) {
            pageLoaded = true;
            if (source != null && rootProperties != null) {
                InternalLoadComponent();
            }
        }

        public void Dispose() {
            webView.Dispose();
        }

        public void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            webView.ExecuteScriptFunction(RootObject + "." + methodCall, args);
        }

        public T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return webView.EvaluateScriptFunction<T>(RootObject + "." + methodCall, args);
        }

        public string DefaultStyleSheet { get; set; }

        public string AdditionalModule { get; set; }

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

        private string ToFullUrl(string url) {
            return (url.StartsWith(PathSeparator) || url.Contains(Uri.SchemeDelimiter)) ? url : $"/{userCallingAssembly.GetName().Name}/{url}";
        }

        private static string Quote(string str) {
            return "\"" + str + "\"";
        }

        private static string NormalizeUrl(string url) {
            return url.Replace("\\", PathSeparator);
        }

    }
}
