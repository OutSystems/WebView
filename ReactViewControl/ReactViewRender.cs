using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using WebViewControl;

namespace ReactViewControl {

    internal partial class ReactViewRender : UserControl, IReactView, IExecutionEngine {

        private const string ModulesObjectName = "__Modules__";
        private const string ComponentLoadedEventName = "ComponentLoaded";

        internal static TimeSpan CustomRequestTimeout = TimeSpan.FromSeconds(5);

        private readonly WebView webView;
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private Listener readyEventListener;
        private bool pageLoaded = false;
        private bool componentLoaded = false;
        private IViewModule component;
        private ResourceUrl defaultStyleSheet;
        private IViewModule[] plugins;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        private ConcurrentQueue<Tuple<string, object[]>> pendingScripts = new ConcurrentQueue<Tuple<string, object[]>>();

        public static bool UseEnhancedRenderingEngine { get; set; } = true;

        public ReactViewRender(bool preloadWebView) {
            userCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            webView = new InternalWebView(this, preloadWebView) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };

            var loadedListener = webView.AttachListener(ComponentLoadedEventName);
            loadedListener.Handler += OnReady;

            webView.Navigated += OnWebViewNavigated;
            webView.Disposed += OnWebViewDisposed;
            webView.BeforeResourceLoad += OnWebViewBeforeResourceLoad;

            Content = webView;

            var urlParams = new string[] {
                UseEnhancedRenderingEngine ? "1" : "0",
                new ResourceUrl(typeof(ReactViewResources.Resources).Assembly, ReactViewResources.Resources.LibrariesPath).ToString(),
                ModulesObjectName,
                Listener.EventListenerObjName,
                ComponentLoadedEventName
            };
            
            webView.LoadResource(new ResourceUrl(typeof(ReactViewResources.Resources).Assembly, ReactViewResources.Resources.DefaultUrl + "?" + string.Join("&", urlParams)));
        }

        private void OnReady() {
            IsReady = true;
            while (true) {
                if (pendingScripts.TryDequeue(out var pendingScript)) {
                    webView.ExecuteScriptFunctionWithSerializedParams(pendingScript.Item1, pendingScript.Item2);
                } else {
                    // nothing else to execute
                    break;
                }
            }
        }

        private void OnWebViewDisposed() {
            Dispose();
        }

        public void Dispose() {
            fileSystemWatcher?.Dispose();
            webView.Dispose();
        }

        public event Action Ready {
            add {
                if (readyEventListener == null) {
                    readyEventListener = webView.AttachListener(ComponentLoadedEventName);
                }
                readyEventListener.UIHandler += value;
            }
            remove {
                if (readyEventListener != null) {
                    readyEventListener.UIHandler -= value;
                }
            }
        }

        public event Action<UnhandledAsyncExceptionEventArgs> UnhandledAsyncException {
            add { webView.UnhandledAsyncException += value; }
            remove { webView.UnhandledAsyncException -= value; }
        }

        public event Func<string, Stream> CustomResourceRequested;

        public void LoadComponent(IViewModule component) {
            this.component = component;
            if (pageLoaded) {
                InternalLoadComponent();
            }
        }

        private void InternalLoadComponent() {
            var source = NormalizeUrl(component.JavascriptSource);
            var baseUrl = ToFullUrl(VirtualPathUtility.GetDirectory(source));

            var loadArgs = new List<string>() {
                JavascriptSerializer.Serialize(baseUrl),
                JavascriptSerializer.Serialize(new [] { component.NativeObjectName, component.Name, source })
            };

            if (DefaultStyleSheet != null) {
                loadArgs.Add(JavascriptSerializer.Serialize(NormalizeUrl(ToFullUrl(DefaultStyleSheet.ToString()))));
            } else {
                loadArgs.Add(JavascriptSerializer.Serialize((string)null));
            }

            loadArgs.Add(JavascriptSerializer.Serialize(enableDebugMode));
            loadArgs.Add(JavascriptSerializer.Serialize(cacheInvalidationTimestamp));

            webView.RegisterJavascriptObject(component.NativeObjectName, component.CreateNativeObject(), executeCallsInUI: false);

            if (Plugins?.Length > 0) {
                // plugins
                var pluginsWithNativeObject = Plugins.Where(p => !string.IsNullOrEmpty(p.NativeObjectName)).ToArray();
                loadArgs.Add(JavascriptSerializer.Serialize(pluginsWithNativeObject.Select(m => new[] { m.Name, m.NativeObjectName })));

                foreach (var module in pluginsWithNativeObject) {
                    webView.RegisterJavascriptObject(module.NativeObjectName, module.CreateNativeObject(), executeCallsInUI: false);
                }

                // mappings
                loadArgs.Add(JavascriptSerializer.Serialize(Plugins.Select(m => new KeyValuePair<string, object>(m.Name, NormalizeUrl(ToFullUrl(m.JavascriptSource))))));
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

        public void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            var method = ModulesObjectName + "." + module.Name + "." + methodCall;
            if (IsReady) {
                webView.ExecuteScriptFunctionWithSerializedParams(method, args);
            } else {
                pendingScripts.Enqueue(Tuple.Create(method, args));
            }
        }

        public T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) {
            return webView.EvaluateScriptFunctionWithSerializedParams<T>(ModulesObjectName + "." + module.Name + "." + methodCall, args);
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
                var invalidPlugins = value.Where(p => string.IsNullOrEmpty(p.JavascriptSource) || string.IsNullOrEmpty(p.Name));
                if (invalidPlugins.Any()) {
                    var pluginName = invalidPlugins.First().Name + "|" + invalidPlugins.First().GetType().Name;
                    throw new ArgumentException($"Plugin '{pluginName}' is invalid");
                }
                plugins = value;
                foreach(var plugin in plugins) {
                    plugin.Bind(this);
                }
            }
        }

        public T WithPlugin<T>() {
            return Plugins.OfType<T>().First();
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

        public double ZoomPercentage {
            get { return webView.ZoomPercentage; }
            set { webView.ZoomPercentage = value; }
        }

        private void ShowResourceLoadFailedMessage(string url) {
            ShowErrorMessage("Failed to load resource '" + url + "'. Press F12 to open developer tools and see more details.");
        }

        private void ShowErrorMessage(string msg) {
            msg = msg.Replace("\"", "\\\"");
            ExecuteDeferredScriptFunction("showErrorMessage", JavascriptSerializer.Serialize(msg));
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

            if (!Directory.Exists(baseLocation)) {
                return;
            }

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
                        if (IsReady && !IsDisposing) {
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

        internal bool IsDisposing => webView.IsDisposing;
    }
}
