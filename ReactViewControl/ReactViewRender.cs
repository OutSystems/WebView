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

        private WebView WebView { get; }
        private Assembly UserCallingAssembly { get; }

        private bool enableDebugMode = false;
        private LoadStatus status;
        private ResourceUrl defaultStyleSheet;
        private IViewModule[] plugins;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        public ReactViewRender(ResourceUrl defaultStyleSheet, IViewModule[] plugins, bool preloadWebView, bool enableDebugMode) {
            UserCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            WebView = new InternalWebView(this, preloadWebView) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };

            DefaultStyleSheet = defaultStyleSheet;
            Plugins = plugins;
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

        public event CustomResourceRequestedEventHandler CustomResourceRequested;

        /// <summary>
        /// Handle external resource requests. 
        /// Call <see cref="WebView.ResourceHandler.BeginAsyncResponse"/> to handle the request in an async way.
        /// </summary>
        public event ExternalResourceRequestedEventHandler ExternalResourceRequested;

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
            var source = NormalizeUrl(component.MainSource);
            var sourceURL = ToFullUrl(component.MainSource.Replace("\\", ResourceUrl.PathSeparator));
            var originalSourceFolder = ToFullUrl(NormalizeUrl(component.OriginalSourceFolder));
            var externalScripts = component.ExternalSources.Select(s => ToFullUrl(s.Replace("\\", ResourceUrl.PathSeparator))).ToArray();

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
                JavascriptSerializer.Serialize(ReactView.PreloadedCacheEntriesSize),
                JavascriptSerializer.Serialize(DefaultStyleSheet != null),
                JavascriptSerializer.Serialize(Plugins?.Length > 0),
                componentSerialization,
                JavascriptSerializer.Serialize(componentHash),
                JavascriptSerializer.Serialize(sourceURL),
                JavascriptSerializer.Serialize(originalSourceFolder),
                JavascriptSerializer.Serialize(externalScripts)
            };

            RegisterNativeObject(component, frameName);

            ExecuteLoaderFunction("loadComponent", frameName, loadArgs);

            if (frameName == WebView.MainFrameName) {
                IsMainComponentLoaded = true;
            }
        }

        private void InternalLoadDefaultStyleSheet(string frameName) {
            var loadArg = JavascriptSerializer.Serialize(DefaultStyleSheet != null ? NormalizeUrl(ToFullUrl(DefaultStyleSheet.ToString())) : null);
            ExecuteLoaderFunction("loadStyleSheet", frameName, loadArg);
        }

        private string GetMappings() {
            return JavascriptSerializer.Serialize(Plugins.Select(m => new KeyValuePair<string, object>(m.Name, NormalizeUrl(ToFullUrl(m.MainSource)))));
        }

        private void InternalLoadPlugins(string frameName) {
            var pluginsWithNativeObject = Plugins.Where(p => !string.IsNullOrEmpty(p.NativeObjectName)).ToArray();
            var loadArgs = new[] {
                JavascriptSerializer.Serialize(pluginsWithNativeObject.Select(m => new[] { m.Name, GetNativeObjectFullName(m.NativeObjectName, frameName) })), // plugins
                JavascriptSerializer.Serialize(Plugins.Select(m => new KeyValuePair<string, object>(m.Name, ToFullUrl(VirtualPathUtility.GetDirectory(NormalizeUrl(m.MainSource)))))),
                GetMappings()
            };

            foreach (var module in pluginsWithNativeObject) {
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
            if (DefaultStyleSheet != null) {
                InternalLoadDefaultStyleSheet(frameName);
            }
            if (Plugins?.Length > 0) {
                InternalLoadPlugins(frameName);
            }
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

        public IViewModule[] Plugins {
            get { return plugins; }
            internal set {
                if (IsMainComponentLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(Plugins)} after component has been loaded");
                }
                var invalidPlugins = value.Where(p => string.IsNullOrEmpty(p.MainSource) || string.IsNullOrEmpty(p.Name));
                if (invalidPlugins.Any()) {
                    var pluginName = invalidPlugins.First().Name + "|" + invalidPlugins.First().GetType().Name;
                    throw new ArgumentException($"Plugin '{pluginName}' is invalid");
                }
                plugins = value;
                foreach(var plugin in plugins) {
                    BindModule(plugin, WebView.MainFrameName);
                }
            }
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

        public T WithPlugin<T>() {
            return Plugins.OfType<T>().First();
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
            const string JsExtension = ".js";

            if (url.EndsWith(JsExtension)) {
                url = url.Substring(0, url.Length - JsExtension.Length); // prevents modules from being loaded twice (once with extension and other without)
            }

            return url.Replace("\\", ResourceUrl.PathSeparator);
        }

        private void OnWebViewBeforeResourceLoad(WebView.ResourceHandler resourceHandler) {
            if (resourceHandler.Url.StartsWith(ResourceUrl.CustomScheme + Uri.SchemeDelimiter)) {
                var customResourceRequested = CustomResourceRequested;
                if (customResourceRequested != null) {
                    resourceHandler.BeginAsyncResponse(() => {
                        var url = resourceHandler.Url;
                        var response = customResourceRequested(url);

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
            } else if (resourceHandler.Url.StartsWith(Uri.UriSchemeHttp) || resourceHandler.Url.StartsWith(Uri.UriSchemeHttps)) {
                ExternalResourceRequested?.Invoke(resourceHandler);
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
    }
}
