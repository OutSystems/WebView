using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using CefSharp;

namespace WebViewControl {

    internal partial class ReactViewRender : ContentControl, IReactView {

        private const string PathSeparator = "/";
        private const string RootObject = "__Root__";
        private const string ReadyEventName = "Ready";

        private static readonly string AssemblyName = typeof(ReactViewRender).Assembly.GetName().Name;
        private static readonly string BuiltinResourcesPath = $"{AssemblyName}/Resources/";
        private static readonly string DefaultUrl = $"{BuiltinResourcesPath}index.html";
        private static readonly string LibrariesPath = $"/{BuiltinResourcesPath}node_modules/";

        private readonly WebView webView = new InternalWebView();
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private Listener readyEventListener;
        private bool pageLoaded = false;
        private string componentSource;
        private string componentJavascriptName;
        private object component;

        public static bool UseEnhancedRenderingEngine { get; set; } = true;

        public ReactViewRender() {
            userCallingAssembly = WebView.GetUserCallingAssembly();

            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener(ReadyEventName, () => IsReady = true, executeInUI: false);
            webView.Navigated += OnWebViewNavigated;

            Content = webView;

            var urlParams = new string[] {
                UseEnhancedRenderingEngine ? "1" : "0",
                LibrariesPath,
                RootObject,
                Listener.EventListenerObjName,
                ReadyEventName
            };

            webView.Address = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultUrl + "?" + string.Join("&", urlParams));
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

        public void LoadComponent(string componentSource, string componentJavascriptName, object component) {
            this.componentSource = componentSource;
            this.componentJavascriptName = componentJavascriptName;
            this.component = component;
            if (pageLoaded) {
                InternalLoadComponent();
            }
        }

        private void InternalLoadComponent() {
            var source = NormalizeUrl(componentSource);
            var filenameParts = source.Split(new[] { PathSeparator }, StringSplitOptions.None);

            // eg: example/dist/source.js
            // baseUrl = /AssemblyName/example/
            var sourceDepth = filenameParts.Length >= 2 ? 2 : 1;
            var baseUrl = ToFullUrl(string.Join(PathSeparator, filenameParts.Take(filenameParts.Length - sourceDepth))) + PathSeparator;

            var loadArgs = new List<string>() {
                Quote(baseUrl),
                Array(Quote(componentJavascriptName), Quote(source))
            };

            if (DefaultStyleSheet != null) {
                loadArgs.Add(Quote(NormalizeUrl(ToFullUrl(DefaultStyleSheet))));
            } else {
                loadArgs.Add("null");
            }

            loadArgs.Add(enableDebugMode ? "true" : "false");

            webView.RegisterJavascriptObject(componentJavascriptName, component, executeCallsInUI: false);

            if (Modules != null && Modules.Length > 0) {
                loadArgs.Add(Array(Modules.Select(m => Array(Quote(m.JavascriptName), Quote(NormalizeUrl(ToFullUrl(m.JavascriptSource)))))));
                foreach (var module in Modules) {
                    webView.RegisterJavascriptObject(module.JavascriptName, module.CreateNativeObject(), executeCallsInUI: false);
                }
            }

            webView.ExecuteScriptFunction("load", loadArgs.ToArray());
        }

        private void OnWebViewNavigated(string obj) {
            pageLoaded = true;
            if (component != null) {
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

        public IViewModule[] Modules { get; set; }

        public bool IsReady { get; private set; }

        public void ShowDeveloperTools() {
            webView.ShowDeveloperTools();
        }

        public void CloseDeveloperTools() {
            webView.CloseDeveloperTools();
        }

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

        private static string Array(params string[] elements) {
            return "[" + string.Join(",", elements) + "]";
        }

        private static string Array(IEnumerable<string> elements) {
            return Array(elements.ToArray());
        }

        private static string NormalizeUrl(string url) {
            const string JsExtension = ".js";

            if (url.EndsWith(JsExtension)) {
                url = url.Substring(0, url.Length - JsExtension.Length); // prevents modules from being loaded twice (once with extension and other without)
            }

            return url.Replace("\\", PathSeparator);
        }
    }
}
