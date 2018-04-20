using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WebViewControl {

    internal partial class ReactViewRender : UserControl, IReactView {

        private const string JavascriptNullConstant = "null";
        
        private const string RootObject = "__Root__";
        private const string ReadyEventName = "Ready";

        internal static TimeSpan CustomRequestTimeout = TimeSpan.FromSeconds(5);

        private static readonly Assembly Assembly = typeof(ReactViewRender).Assembly;
        private static readonly string BuiltinResourcesPath = "Resources/";
        private static readonly string DefaultUrl = $"{BuiltinResourcesPath}index.html";
        private static readonly string LibrariesPath = new ResourceUrl(Assembly, $"{BuiltinResourcesPath}node_modules/").ToString();

        private readonly WebView webView;
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private Listener readyEventListener;
        private bool pageLoaded = false;
        private bool componentLoaded = false;
        private string componentSource;
        private string componentJavascriptName;
        private object component;
        private ResourceUrl defaultStyleSheet;
        private IViewModule[] plugins;
        private Dictionary<string, ResourceUrl> mappings;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        public static bool UseEnhancedRenderingEngine { get; set; } = true;

        public ReactViewRender() {
            userCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            webView = new InternalWebView(this) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };
            webView.AttachListener(ReadyEventName, () => IsReady = true, executeInUI: false);
            webView.Navigated += OnWebViewNavigated;
            webView.Disposed += OnWebViewDisposed;
            webView.BeforeResourceLoad += OnWebViewBeforeResourceLoad;

            Content = webView;

            var urlParams = new string[] {
                UseEnhancedRenderingEngine ? "1" : "0",
                LibrariesPath,
                RootObject,
                Listener.EventListenerObjName,
                ReadyEventName
            };
            
            webView.LoadResource(new ResourceUrl(typeof(ReactViewRender).Assembly, DefaultUrl + "?" + string.Join("&", urlParams)));
        }

        private void OnWebViewDisposed() {
            Dispose();
        }

        public void Dispose() {
            fileSystemWatcher?.Dispose();
            webView.Dispose();
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

        public event Func<string, Stream> CustomResourceRequested;

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
            var filenameParts = source.Split(new[] { ResourceUrl.PathSeparator }, StringSplitOptions.None);

            // eg: example/dist/source.js
            // baseUrl = /AssemblyName/example/
            var sourceDepth = filenameParts.Length >= 2 ? 2 : 1;
            var baseUrl = ToFullUrl(string.Join(ResourceUrl.PathSeparator, filenameParts.Take(filenameParts.Length - sourceDepth))) + ResourceUrl.PathSeparator;

            var loadArgs = new List<string>() {
                Quote(baseUrl),
                Array(Quote(componentJavascriptName), Quote(source))
            };

            if (DefaultStyleSheet != null) {
                loadArgs.Add(Quote(NormalizeUrl(ToFullUrl(DefaultStyleSheet.ToString()))));
            } else {
                loadArgs.Add(JavascriptNullConstant);
            }

            loadArgs.Add(AsBoolean(enableDebugMode));
            loadArgs.Add(Quote(cacheInvalidationTimestamp));

            webView.RegisterJavascriptObject(componentJavascriptName, component, executeCallsInUI: false);

            if (Plugins != null && Plugins.Length > 0) {
                loadArgs.Add(Array(Plugins.Select(m => Array(Quote(m.JavascriptName), Quote(NormalizeUrl(ToFullUrl(m.JavascriptSource)))))));
                foreach (var module in Plugins) {
                    webView.RegisterJavascriptObject(module.JavascriptName, module.CreateNativeObject(), executeCallsInUI: false);
                }
            } else {
                loadArgs.Add(JavascriptNullConstant);
            }

            if (Mappings != null && Mappings.Count > 0) {
                loadArgs.Add(Object(Mappings.Select(m => new KeyValuePair<string, string>(Quote(m.Key), Quote(NormalizeUrl(m.Value.ToString()))))));
            }

            ExecuteDeferredScriptFunction("load", loadArgs.ToArray());
            componentLoaded = true;
        }

        private void OnWebViewNavigated(string obj) {
            IsReady = false;
            pageLoaded = true;
            if (component != null) {
                InternalLoadComponent();
            }
        }

        public void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            webView.ExecuteScriptFunction(RootObject + "." + methodCall, args);
        }

        public T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return webView.EvaluateScriptFunction<T>(RootObject + "." + methodCall, args);
        }
        
        public ResourceUrl DefaultStyleSheet {
            get { return defaultStyleSheet; }
            set {
                if (componentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(DefaultStyleSheet)} after component has been loaded");
                }
                defaultStyleSheet = value;
            }
        }

        public IViewModule[] Plugins {
            get { return plugins; }
            set {
                if (componentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(Plugins)} after component has been loaded");
                }
                plugins = value;
            }
        }

        public Dictionary<string, ResourceUrl> Mappings {
            get { return mappings; }
            set {
                if (componentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(Mappings)} after component has been loaded");
                }
                mappings = value;
            }
        }

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
            ExecuteDeferredScriptFunction("showErrorMessage", Quote(msg));
        }
        
        private string ToFullUrl(string url) {
            if (url.Contains(Uri.SchemeDelimiter)) {
                return url;
            } else if (url.StartsWith(ResourceUrl.PathSeparator)) {
                return new ResourceUrl(ResourceUrl.EmbeddedScheme, url).ToString();
            } else {
                return new ResourceUrl(userCallingAssembly, url).ToString();
            }
        }

        public void EnableHotReload(string baseLocation) {
            if (string.IsNullOrEmpty(baseLocation)) {
                throw new InvalidOperationException("Hot reload does not work in release mode");
            }

            baseLocation = Path.GetDirectoryName(baseLocation);
            baseLocation = Path.GetFullPath(baseLocation + "\\..\\.."); // get up 2 levels (.../View/src -> .../)

            if (fileSystemWatcher != null) {
                fileSystemWatcher.Path = baseLocation;
                return;
            }

            fileSystemWatcher = new FileSystemWatcher(baseLocation);
            fileSystemWatcher.IncludeSubdirectories = true;
            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
            fileSystemWatcher.EnableRaisingEvents = true;

            var filesChanged = false;
            var fileExtensionsToWatch = new[] { ".js", ".css" };

            fileSystemWatcher.Changed += (sender, eventArgs) => {
                if (IsReady) {
                    // TODO visual studio reports a change in a file with a (strange) temporary name
                    //if (fileExtensionsToWatch.Any(e => eventArgs.Name.EndsWith(e))) {
                    filesChanged = true;
                    webView.Dispatcher.BeginInvoke((Action) (() => {
                        if (IsReady) {
                            IsReady = false;
                            cacheInvalidationTimestamp = DateTime.UtcNow.Ticks.ToString();
                            webView.Reload(true);
                        }
                    }));
                    //}
                }
            };
            webView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => {
                if (filesChanged) {
                    var url = new Uri(resourceHandler.Url);
                    var path = Path.Combine(ResourceUrl.GetEmbeddedResourcePath(url).Skip(1).ToArray()); // skip first part (namespace)
                    if (fileExtensionsToWatch.Any(e => path.EndsWith(e))) {
                        path = Path.Combine(fileSystemWatcher.Path, path);
                        var file = new FileInfo(path);
                        if (file.Exists) {
                            resourceHandler.RespondWith(path);
                        }
                    }
                }
            };
        }

        private void ExecuteDeferredScriptFunction(string functionName, params string[] args) {
            // using setimeout we make sure the function is already defined
            webView.ExecuteScript($"setTimeout(() => {functionName}({string.Join(",", args)}), 0)");
        }

        private static string Quote(string str) {
            return "\"" + str + "\"";
        }

        private static string AsBoolean(bool value) {
            return value ? "true" : "false";
        }

        private static string Array(params string[] elements) {
            return "[" + string.Join(",", elements) + "]";
        }

        private static string Array(IEnumerable<string> elements) {
            return Array(elements.ToArray());
        }

        private static string Object(IEnumerable<KeyValuePair<string, string>> properties) {
            return "{" + string.Join(",", properties.Select(p => p.Key + ":" + p.Value)) + "}";
        }

        private static string NormalizeUrl(string url) {
            const string JsExtension = ".js";

            if (url.EndsWith(JsExtension)) {
                url = url.Substring(0, url.Length - JsExtension.Length); // prevents modules from being loaded twice (once with extension and other without)
            }

            return url.Replace("\\", ResourceUrl.PathSeparator);
        }

        private void OnWebViewBeforeResourceLoad(WebView.ResourceHandler resourceHandler) {
            var customResourceRequested = CustomResourceRequested;
            if (customResourceRequested != null) {
                if (resourceHandler.Url.StartsWith(ResourceUrl.CustomScheme + Uri.SchemeDelimiter)) {
                    var customResourceFetchTask = Task.Run(() => customResourceRequested(resourceHandler.Url));
                    customResourceFetchTask.Wait(CustomRequestTimeout);
                    if (!customResourceFetchTask.IsCompleted) {
                        throw new Exception($"Failed to fetch ({resourceHandler.Url}) within the alotted timeout");
                    }
                    resourceHandler.RespondWith(customResourceFetchTask.Result, "");
                }
            }
        }

        internal IInputElement FocusableElement {
            get { return webView.FocusableElement; }
        }
    }
}
