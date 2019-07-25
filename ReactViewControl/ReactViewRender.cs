using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using WebViewControl;

namespace ReactViewControl {

    internal partial class ReactViewRender : UserControl, IDisposable {

        private enum LoadStatus {
            Initialized,
            PageLoaded,
            Ready
        }

        internal const string ModulesObjectName = "__Modules__";
        private const string ComponentLoadedEventName = "ComponentLoaded";

        private static Assembly ResourcesAssembly { get; } = typeof(ReactViewResources.Resources).Assembly;

        private Dictionary<string, IViewModule> FrameToComponentMap { get; } = new Dictionary<string, IViewModule>();
        private Dictionary<string, ExecutionEngine> FrameToExecutionEngineMap { get; } = new Dictionary<string, ExecutionEngine>();
        private Dictionary<string, IViewModule[]> FrameToPluginsMap { get; } = new Dictionary<string, IViewModule[]>();

        private WebView WebView { get; }
        private Assembly UserCallingAssembly { get; }

        private bool enableDebugMode = false;
        private LoadStatus status;
        private ResourceUrl defaultStyleSheet;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        public ReactViewRender(ResourceUrl defaultStyleSheet, IViewModule[] plugins, bool preloadWebView, bool enableDebugMode) {
            UserCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            WebView = new InternalWebView(this, preloadWebView) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };

            DefaultStyleSheet = defaultStyleSheet;
            AddPlugins(WebView.MainFrameName, plugins);
            EnableDebugMode = enableDebugMode;

            var loadedListener = WebView.AttachListener(ComponentLoadedEventName);
            loadedListener.Handler += OnReady;
            loadedListener.UIHandler += OnReadyUIHandler;

            WebView.Navigated += OnWebViewNavigated;
            WebView.Disposed += OnWebViewDisposed;
            WebView.BeforeResourceLoad += OnWebViewBeforeResourceLoad;
            WebView.JavascriptContextReleased += OnWebViewJavascriptContextReleased;
            Content = WebView;

            var urlParams = new string[] {
                new ResourceUrl(ResourcesAssembly).ToString(),
                enableDebugMode ? "1" : "0",
                ModulesObjectName,
                Listener.EventListenerObjName,
                ComponentLoadedEventName
            };

            WebView.LoadResource(new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.DefaultUrl + "?" + string.Join("&", urlParams)));
        }

        private void OnReady(params object[] args) {
            var frameName = (string) args.FirstOrDefault();
            if (frameName == WebView.MainFrameName) {
                status = LoadStatus.Ready;
            }
            if (FrameToComponentMap.TryGetValue(frameName, out var component)) {
                if (component.Engine is ExecutionEngine engine) {
                    engine.Start();
                }
            }
        }

        private void OnReadyUIHandler(object[] args) {
            Ready?.Invoke();
        }

        private void OnWebViewJavascriptContextReleased(string frameName) {
            lock (FrameToExecutionEngineMap) {
                FrameToExecutionEngineMap.Remove(frameName);
            }
        }

        private void OnWebViewDisposed() {
            Dispose();
        }

        public void Dispose() {
            fileSystemWatcher?.Dispose();
            WebView.Dispose();
        }

        public event Action Ready;

        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { WebView.UnhandledAsyncException += value; }
            remove { WebView.UnhandledAsyncException -= value; }
        }

        public event ResourceLoadFailedEventHandler ResourceLoadFailed {
            add { WebView.ResourceLoadFailed += value; }
            remove { WebView.ResourceLoadFailed -= value; }
        }

        /// <summary>
        /// Handle embedded resource requests. You can use this event to change the resource being loaded.
        /// </summary>
        public event ResourceRequestedEventHandler EmbeddedResourceRequested;

        /// <summary>
        /// Handle custom resource requests. Use this event to load the resource based on the url.
        /// </summary>
        public event CustomResourceRequestedEventHandler CustomResourceRequested;

        /// <summary>
        /// Handle external resource requests. 
        /// Call <see cref="WebView.ResourceHandler.BeginAsyncResponse"/> to handle the request in an async way.
        /// </summary>
        public event ResourceRequestedEventHandler ExternalResourceRequested;

        public void LoadComponent(IViewModule component) {
            LoadComponent(component, WebView.MainFrameName);
        }

        public void LoadComponent(IViewModule component, string frameName) {
            FrameToComponentMap[frameName] = component;
            BindModule(component, frameName);
            if (status >= LoadStatus.PageLoaded && WebView.HasFrame(frameName)) {
                InternalLoadComponent(component, frameName);
            }
        }

        private void InternalLoadComponent(IViewModule component, string frameName) {
            var mainSource = ToFullUrl(NormalizeUrl(component.MainJsSource));
            var dependencySources = component.DependencyJsSources.Select(s => ToFullUrl(NormalizeUrl(s))).ToArray();
            var cssSources = component.CssSources.Select(s => ToFullUrl(NormalizeUrl(s))).ToArray();
            var originalSourceFolder = ToFullUrl(NormalizeUrl(component.OriginalSourceFolder));

            var nativeObjectMethodsMap =
                component.Events.Select(g => new KeyValuePair<string, object>(g, JavascriptSerializer.Undefined))
                .Concat(component.PropertiesValues)
                .OrderBy(p => p.Key)
                .Select(p => new KeyValuePair<string, object>(JavascriptSerializer.GetJavascriptName(p.Key), p.Value));
            var componentSerialization = JavascriptSerializer.Serialize(nativeObjectMethodsMap);
            var componentHash = ComputeHash(componentSerialization);

            // loadComponent arguments:
            //
            // componentName: string,
            // componentSource: string,
            // dependencySources: string[],
            // cssSources: string[],
            // originalSourceFolder: string,
            // componentNativeObjectName: string,
            // hasStyleSheet: boolean,
            // hasPlugins: boolean,
            // componentNativeObject: Dictionary<any>,
            // hash: string

            var loadArgs = new [] {
                JavascriptSerializer.Serialize(component.Name),
                JavascriptSerializer.Serialize(GetNativeObjectFullName(component.NativeObjectName, frameName)),
                JavascriptSerializer.Serialize(mainSource),
                JavascriptSerializer.Serialize(dependencySources),
                JavascriptSerializer.Serialize(cssSources),
                JavascriptSerializer.Serialize(originalSourceFolder),
                JavascriptSerializer.Serialize(ReactView.PreloadedCacheEntriesSize),
                JavascriptSerializer.Serialize(DefaultStyleSheet != null),
                JavascriptSerializer.Serialize(GetPlugins(frameName).Length > 0),
                componentSerialization,
                JavascriptSerializer.Serialize(componentHash)
            };

            RegisterNativeObject(component, frameName);

            ExecuteLoaderFunction("loadComponent", frameName, loadArgs);

            if (frameName == WebView.MainFrameName) {
                IsMainComponentLoaded = true;
            }
        }

        private void InternalLoadDefaultStyleSheet(string frameName) {
            if (DefaultStyleSheet != null) {
                var loadArg = JavascriptSerializer.Serialize(NormalizeUrl(ToFullUrl(DefaultStyleSheet.ToString())));
                ExecuteLoaderFunction("loadDefaultStyleSheet", frameName, loadArg);
            }
        }

        private void InternalLoadPlugins(string frameName) {
            var plugins = GetPlugins(frameName);
            if (plugins.Length == 0) {
                return;
            }

            var loadArgs = new[] {
                JavascriptSerializer.Serialize(plugins.Select(m => new object[] {
                    m.Name,
                    GetNativeObjectFullName(m.NativeObjectName, frameName),
                    m.NativeObjectName,
                    ToFullUrl(NormalizeUrl(m.MainJsSource)),
                    m.DependencyJsSources.Select(s => ToFullUrl(NormalizeUrl(s)))
                }))
            };

            foreach (var module in plugins) {
                RegisterNativeObject(module, frameName);
            }

            ExecuteLoaderFunction("loadPlugins", frameName, loadArgs);
        }

        public bool IsMainComponentLoaded { get; private set; }

        private void OnWebViewNavigated(string url, string frameName) {
            if (!url.StartsWith(ResourceUrl.EmbeddedScheme + Uri.SchemeDelimiter)) {
                // not a component, maybe its an iframe with an external url, bail out
                return;
            }

            if (frameName == WebView.MainFrameName) {
                status = LoadStatus.PageLoaded;
            }
            
            InternalLoadDefaultStyleSheet(frameName);
            InternalLoadPlugins(frameName);

            if (FrameToComponentMap.TryGetValue(frameName, out var component)) { 
                InternalLoadComponent(component, frameName);
            }
        }

        
        public ResourceUrl DefaultStyleSheet {
            get { return defaultStyleSheet; }
            private set {
                if (IsMainComponentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(DefaultStyleSheet)} after component has been loaded");
                }
                defaultStyleSheet = value;
            }
        }

        public void AddPlugins(string frameName, params IViewModule[] plugins) {
            if (frameName == WebView.MainFrameName && IsMainComponentLoaded) {
                throw new InvalidOperationException($"Cannot add plugins after component has been loaded");
            }
            var invalidPlugins = plugins.Where(p => string.IsNullOrEmpty(p.MainJsSource) || string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.NativeObjectName));
            if (invalidPlugins.Any()) {
                var pluginName = invalidPlugins.First().Name + "|" + invalidPlugins.First().GetType().Name;
                throw new ArgumentException($"Plugin '{pluginName}' is invalid");
            }

            FrameToPluginsMap[frameName] = GetPlugins(frameName).Concat(plugins).ToArray();

            foreach (var plugin in plugins) {
                BindModule(plugin, WebView.MainFrameName);
            }
        }

        public void ClearPlugins(string frameName) {
            FrameToPluginsMap.Remove(frameName);
        }

        internal void BindModule(IViewModule module, string frameName) {
            ExecutionEngine engine;
            lock (FrameToExecutionEngineMap) {
                if (!FrameToExecutionEngineMap.TryGetValue(frameName, out engine)) {
                    engine = new ExecutionEngine(WebView, frameName);
                    FrameToExecutionEngineMap[frameName] = engine;
                }
            }
            module.Bind(engine);
        }

        public T WithPlugin<T>(string frameName = WebView.MainFrameName) {
            var plugin = GetPlugins(frameName).OfType<T>().FirstOrDefault();
            if (plugin == null) {
                throw new InvalidOperationException($"Plugin {typeof(T).Name} not found in {frameName}");
            }
            return plugin;
        }

        public bool IsReady => status == LoadStatus.Ready;

        public void ShowDeveloperTools() {
            WebView.ShowDeveloperTools();
        }

        public void CloseDeveloperTools() {
            WebView.CloseDeveloperTools();
        }

        public bool EnableDebugMode {
            get { return enableDebugMode; }
            set {
                enableDebugMode = value;
                WebView.AllowDeveloperTools = enableDebugMode;
                if (enableDebugMode) {
                    WebView.ResourceLoadFailed += ShowResourceLoadFailedMessage;
                } else {
                    WebView.ResourceLoadFailed -= ShowResourceLoadFailedMessage;
                }
            }
        }

        public double ZoomPercentage {
            get { return WebView.ZoomPercentage; }
            set { WebView.ZoomPercentage = value; }
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
                return new ResourceUrl(UserCallingAssembly, url).ToString();
            }
        }

        public void EnableHotReload(string mainModuleFullPath, string mainModuleResourcePath) {
            if (string.IsNullOrEmpty(mainModuleFullPath)) {
                throw new InvalidOperationException("Hot reload does not work in release mode");
            }

            var basePath = Path.GetDirectoryName(mainModuleFullPath);
            var mainModuleResourcePathParts = ResourceUrl.GetEmbeddedResourcePath(new Uri(ToFullUrl(VirtualPathUtility.GetDirectory(mainModuleResourcePath))));

            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), mainModuleResourcePathParts);

            if (!Directory.Exists(basePath) || !basePath.EndsWith(relativePath)) {
                return;
            }

            if (fileSystemWatcher != null) {
                fileSystemWatcher.Path = basePath;
                return;
            }

            fileSystemWatcher = new FileSystemWatcher(basePath);
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
                            status = LoadStatus.Initialized;
                            cacheInvalidationTimestamp = DateTime.UtcNow.Ticks.ToString();
                            WebView.Reload(true);
                        }
                    }));
                    //}
                }
            };
            WebView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => {
                if (filesChanged) {
                    var url = new Uri(resourceHandler.Url);
                    var resourcePath = ResourceUrl.GetEmbeddedResourcePath(url);
                    var path = Path.Combine(resourcePath.Skip(mainModuleResourcePathParts.Length).ToArray());
                    if (fileExtensionsToWatch.Any(e => path.EndsWith(e))) {
                        path = Path.Combine(fileSystemWatcher.Path, path);
                        var file = new FileInfo(path);
                        if (file.Exists) {
                            resourceHandler.RespondWith(path);
                        } else {
                            System.Diagnostics.Debug.WriteLine("File not found: " + file.FullName + " (" + resourceHandler.Url + ")");
                        }
                    }
                }
            };
        }

        private void ExecuteLoaderFunction(string functionName, string frameName, params string[] args) {
            // using setimeout we make sure the function is already defined
            var loaderUrl = new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.LoaderUrl);
            WebView.ExecuteScript($"import('{loaderUrl}').then(m => m.{functionName}({string.Join(",", args)}))", frameName);
        }
        
        private static string NormalizeUrl(string url) {
            return url.Replace("\\", ResourceUrl.PathSeparator);
        }

        private void OnWebViewBeforeResourceLoad(WebView.ResourceHandler resourceHandler) {
            var url = resourceHandler.Url;
            var scheme = url.Substring(0, Math.Max(0, url.IndexOf(Uri.SchemeDelimiter)));

            switch (scheme.ToLowerInvariant()) {
                case ResourceUrl.CustomScheme:
                    var customResourceRequestedHandlers = CustomResourceRequested?.GetInvocationList().ToArray();
                    if (customResourceRequestedHandlers?.Any() == true) {
                        resourceHandler.BeginAsyncResponse(() => {
                            var response = customResourceRequestedHandlers.Cast<CustomResourceRequestedEventHandler>().Select(h => h(url)).FirstOrDefault(r => r != null);

                            if (response != null) {
                                string extension = null;
                                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                                    var path = uri.AbsolutePath;
                                    extension = Path.GetExtension(path).TrimStart('.');
                                }

                                resourceHandler.RespondWith(response, extension);
                            }
                        });
                    }
                    break;

                case ResourceUrl.EmbeddedScheme:
                    // webview already started BeginAsyncResponse
                    EmbeddedResourceRequested?.Invoke(resourceHandler);
                    break;

                case "http":
                case "https":
                    ExternalResourceRequested?.Invoke(resourceHandler);
                    break;
            }
        }

        internal IInputElement FocusableElement {
            get { return WebView.FocusableElement; }
        }

        internal bool IsDisposing => WebView.IsDisposing;

        private static string ComputeHash(string inputString) {
            using (var sha256 = SHA256.Create()) {
                return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString)));
            }
        }

        private void RegisterNativeObject(IViewModule module, string frameName) {
            WebView.RegisterJavascriptObject(GetNativeObjectFullName(module.NativeObjectName, frameName), module.CreateNativeObject(), executeCallsInUI: false);
        }

        private static string GetNativeObjectFullName(string name, string frameName) {
            return (frameName == WebView.MainFrameName ? frameName : frameName + "$") + name;
        }

        private IViewModule[] GetPlugins(string frameName) {
            FrameToPluginsMap.TryGetValue(frameName, out var plugins);
            return plugins ?? new IViewModule[0];
        }
    }
}
