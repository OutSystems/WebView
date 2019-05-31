using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using WebViewControl;

namespace ReactViewControl {

    internal partial class ReactViewRender : UserControl, IExecutionEngine, IDisposable {

        private const string ModulesObjectName = "__Modules__";
        private const string ComponentLoadedEventName = "ComponentLoaded";

        private static readonly Assembly ResourcesAssembly = typeof(ReactViewResources.Resources).Assembly;

        internal static TimeSpan CustomRequestTimeout = TimeSpan.FromSeconds(5);

        private readonly Dictionary<string, IViewModule> components = new Dictionary<string, IViewModule>();
        private readonly WebView webView;
        private Assembly userCallingAssembly;

        private bool enableDebugMode = false;
        private Listener readyEventListener;
        private bool pageLoaded = false;
        private ResourceUrl defaultStyleSheet;
        private IViewModule[] plugins;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        private ConcurrentQueue<Tuple<string, object[]>> pendingScripts = new ConcurrentQueue<Tuple<string, object[]>>();

        public ReactViewRender(ResourceUrl defaultStyleSheet, IViewModule[] plugins, bool preloadWebView, bool enableDebugMode) {
            userCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            webView = new InternalWebView(this, preloadWebView) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };

            DefaultStyleSheet = defaultStyleSheet;
            Plugins = plugins;
            EnableDebugMode = enableDebugMode;

            var loadedListener = webView.AttachListener(ComponentLoadedEventName);
            loadedListener.Handler += OnReady;

            webView.Navigated += OnWebViewNavigated;
            webView.Disposed += OnWebViewDisposed;
            webView.BeforeResourceLoad += OnWebViewBeforeResourceLoad;

            Content = webView;

            var urlParams = new string[] {
                new ResourceUrl(ResourcesAssembly).ToString(),
                enableDebugMode ? "1" : "0",
                ModulesObjectName,
                Listener.EventListenerObjName,
                ComponentLoadedEventName
            };

            webView.LoadResource(new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.DefaultUrl + "?" + string.Join("&", urlParams)));
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

        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { webView.UnhandledAsyncException += value; }
            remove { webView.UnhandledAsyncException -= value; }
        }

        public event ResourceLoadFailedEventHandler ResourceLoadFailed {
            add { webView.ResourceLoadFailed += value; }
            remove { webView.ResourceLoadFailed -= value; }
        }

        public event CustomResourceRequestedEventHandler CustomResourceRequested;

        public void LoadComponent(IViewModule component) {
            LoadComponent(component, "");
        }

        public void LoadComponent(IViewModule component, string frameName) {
            components[frameName] = component;
            if (pageLoaded) {
                InternalLoadComponent(component, frameName);
            }
        }

        private void InternalLoadComponent(IViewModule component, string frameName) {
            var source = NormalizeUrl(component.JavascriptSource);
            var baseUrl = ToFullUrl(VirtualPathUtility.GetDirectory(source));
            var urlSuffix = cacheInvalidationTimestamp != null ? "t=" + cacheInvalidationTimestamp : null;

            var nativeObjectMethodsMap =
                component.Events.Select(g => new KeyValuePair<string, object>(g, JavascriptSerializer.Undefined))
                .Concat(component.PropertiesValues)
                .OrderBy(p => p.Key)
                .Select(p => new KeyValuePair<string, object>(JavascriptSerializer.GetJavascriptName(p.Key), p.Value));
            var componentSerialization = JavascriptSerializer.Serialize(nativeObjectMethodsMap);
            var componentHash = ComputeHash(componentSerialization);
            
            // loadComponent arguments:
            // componentName: string,
            // componentSource: string,
            // componentNativeObjectName: string,
            // baseUrl: string,
            // cacheInvalidationSuffix: string,
            // hasStyleSheet: boolean,
            // hasPlugins: boolean,
            // componentNativeObject: Dictionary<any>,
            // hash: string,
            // mappings: Dictionary<string>

            var loadArgs = new [] {
                JavascriptSerializer.Serialize(component.Name),
                JavascriptSerializer.Serialize(source),
                JavascriptSerializer.Serialize(GetNativeObjectFullName(component.NativeObjectName, frameName)),
                JavascriptSerializer.Serialize(baseUrl),
                JavascriptSerializer.Serialize(urlSuffix),
                JavascriptSerializer.Serialize(ReactView.PreloadedCacheEntriesSize),
                JavascriptSerializer.Serialize(DefaultStyleSheet != null),
                JavascriptSerializer.Serialize(Plugins?.Length > 0),
                componentSerialization,
                JavascriptSerializer.Serialize(componentHash),
                GetMappings()
            };

            RegisterNativeObject(component, frameName);
            component.Bind(this);

            ExecuteLoaderFunction("loadComponent", frameName, loadArgs);
            IsComponentLoaded = true;
        }

        private void InternalLoadDefaultStyleSheet(string frameName) {
            var loadArg = JavascriptSerializer.Serialize(DefaultStyleSheet != null ? NormalizeUrl(ToFullUrl(DefaultStyleSheet.ToString())) : null);
            ExecuteLoaderFunction("loadStyleSheet", frameName, loadArg);
        }

        private string GetMappings() {
            return JavascriptSerializer.Serialize(Plugins.Select(m => new KeyValuePair<string, object>(m.Name, NormalizeUrl(ToFullUrl(m.JavascriptSource)))));
        }

        private void InternalLoadPlugins(string frameName) {
            var pluginsWithNativeObject = Plugins.Where(p => !string.IsNullOrEmpty(p.NativeObjectName)).ToArray();
            var loadArgs = new[] {
                JavascriptSerializer.Serialize(pluginsWithNativeObject.Select(m => new[] { m.Name, GetNativeObjectFullName(m.NativeObjectName, frameName) })), // plugins
                GetMappings()
            };

            foreach (var module in pluginsWithNativeObject) {
                RegisterNativeObject(module, frameName);
            }

            ExecuteLoaderFunction("loadPlugins", frameName, loadArgs);
        }

        public bool IsComponentLoaded { get; private set; }

        private void OnWebViewNavigated(string url, string frameName) {
            IsReady = false;
            pageLoaded = true;
            if (DefaultStyleSheet != null) {
                InternalLoadDefaultStyleSheet(frameName);
            }
            if (Plugins?.Length > 0) {
                InternalLoadPlugins(frameName);
            }
            if (components.TryGetValue(frameName, out var component)) { 
                InternalLoadComponent(component, frameName);
            }
        }

        private static string FormatMethodInvocation(IViewModule module, string methodCall) {
            return ModulesObjectName + "[\"" + module.Name + "\"]." + methodCall;
        }

        public void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            var method = FormatMethodInvocation(module, methodCall);
            if (IsReady) {
                webView.ExecuteScriptFunctionWithSerializedParams(method, args);
            } else {
                pendingScripts.Enqueue(Tuple.Create(method, args));
            }
        }

        public T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) {
            var method = FormatMethodInvocation(module, methodCall);
            return webView.EvaluateScriptFunctionWithSerializedParams<T>(method, args);
        }
        
        public ResourceUrl DefaultStyleSheet {
            get { return defaultStyleSheet; }
            private set {
                if (IsComponentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(DefaultStyleSheet)} after component has been loaded");
                }
                defaultStyleSheet = value;
            }
        }

        public IViewModule[] Plugins {
            get { return plugins; }
            internal set {
                if (IsComponentLoaded) {
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
            ExecuteLoaderFunction("showErrorMessage", WebView.MainFrameName, JavascriptSerializer.Serialize(msg));
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
                    Dispatcher.BeginInvoke((Action) (() => {
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

        private void ExecuteLoaderFunction(string functionName, string frameName, params string[] args) {
            // using setimeout we make sure the function is already defined
            var loaderUrl = new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.LoaderUrl);
            webView.ExecuteScript($"import('{loaderUrl}').then(m => m.{functionName}({string.Join(",", args)}))", frameName);
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

        private static string ComputeHash(string inputString) {
            using (var sha256 = SHA256.Create()) {
                return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString)));
            }
        }

        private void RegisterNativeObject(IViewModule module, string frameName) {
            webView.RegisterJavascriptObject(GetNativeObjectFullName(module.NativeObjectName, frameName), module.CreateNativeObject(), executeCallsInUI: false);
        }

        private static string GetNativeObjectFullName(string name, string frameName) {
            return (frameName == WebView.MainFrameName ? frameName : frameName + "$") + name;
        }
    }
}
