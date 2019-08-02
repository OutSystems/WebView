using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using WebViewControl;

namespace ReactViewControl {

    internal partial class ReactViewRender : UserControl, IDisposable {

        private enum LoadStatus {
            Initialized,
            PageLoaded, // page is loaded but component not loaded yet
            ComponentLoading, // component is loading
            Ready // component is ready
        }

        private object SyncRoot { get; } = new object();

        internal const string ModulesObjectName = "__Modules__";
        
        private const string ViewInitializedEventName = "ViewInitialized";
        private const string ViewDestroyedEventName = "ViewDestroyed";
        private const string ViewLoadedEventName = "ViewLoaded";

        private static Assembly ResourcesAssembly { get; } = typeof(ReactViewResources.Resources).Assembly;

        private Dictionary<string, IViewModule> FrameToComponentMap { get; } = new Dictionary<string, IViewModule>();
        private Dictionary<string, ExecutionEngine> FrameToExecutionEngineMap { get; } = new Dictionary<string, ExecutionEngine>();
        private Dictionary<string, IViewModule[]> FrameToPluginsMap { get; } = new Dictionary<string, IViewModule[]>();

        private WebView WebView { get; }
        private Assembly UserCallingAssembly { get; }
        private LoaderModule Loader { get; }
        private Func<IViewModule[]> PluginsFactory { get; }

        private bool enableDebugMode = false;
        private LoadStatus status;
        private ResourceUrl defaultStyleSheet;
        private FileSystemWatcher fileSystemWatcher;
        private string cacheInvalidationTimestamp;

        public ReactViewRender(ResourceUrl defaultStyleSheet, Func<IViewModule[]> initializePlugins, bool preloadWebView, bool enableDebugMode) {
            UserCallingAssembly = WebView.GetUserCallingMethod().ReflectedType.Assembly;

            WebView = new InternalWebView(this, preloadWebView) {
                DisableBuiltinContextMenus = true,
                IgnoreMissingResources = false
            };

            Loader = new LoaderModule(this);

            DefaultStyleSheet = defaultStyleSheet;
            PluginsFactory = initializePlugins;
            AddPlugins(WebView.MainFrameName, initializePlugins());
            EnableDebugMode = enableDebugMode;

            var loadedListener = WebView.AttachListener(ViewLoadedEventName);
            loadedListener.Handler += OnReady;
            loadedListener.UIHandler += OnReadyUIHandler;

            WebView.AttachListener(ViewInitializedEventName).Handler += OnViewInitialized;
            WebView.AttachListener(ViewDestroyedEventName).Handler += OnViewDestroyed;

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
                ViewInitializedEventName,
                ViewDestroyedEventName,
                ViewLoadedEventName
            };

            WebView.LoadResource(new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.DefaultUrl + "?" + string.Join("&", urlParams)));
        }

        public IInputElement FocusableElement => WebView.FocusableElement;

        public bool IsDisposing => WebView.IsDisposing;

        /// <summary>
        /// True when the main component has been rendered.
        /// </summary>
        public bool IsReady => status == LoadStatus.Ready;

        /// <summary>
        /// True when view component is loading or loaded
        /// </summary>
        public bool IsMainComponentLoaded => status >= LoadStatus.ComponentLoading;

        /// <summary>
        /// Enables or disables debug mode. 
        /// In debug mode the webview developer tools becomes available pressing F12 and the webview shows an error message at the top with the error details 
        /// when a resource fails to load.
        /// </summary>
        public bool EnableDebugMode {
            get { return enableDebugMode; }
            set {
                enableDebugMode = value;
                WebView.AllowDeveloperTools = enableDebugMode;
                if (enableDebugMode) {
                    WebView.ResourceLoadFailed += Loader.ShowResourceLoadFailedMessage;
                } else {
                    WebView.ResourceLoadFailed -= Loader.ShowResourceLoadFailedMessage;
                }
            }
        }

        /// <summary>
        /// Gets or sets the webview zoom percentage (1 = 100%)
        /// </summary>
        public double ZoomPercentage {
            get { return WebView.ZoomPercentage; }
            set { WebView.ZoomPercentage = value; }
        }

        /// <summary>
        /// Event fired when the component is rendered and ready for interaction.
        /// </summary>
        public event Action Ready;

        /// <summary>
        /// Event fired when an async exception occurs (eg: executing javascript)
        /// </summary>
        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { WebView.UnhandledAsyncException += value; }
            remove { WebView.UnhandledAsyncException -= value; }
        }

        /// <summary>
        /// Event fired when a resource fails to load.
        /// </summary>
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

        private void OnReady(params object[] args) {
            var frameName = (string)args.FirstOrDefault();

            lock (SyncRoot) {
                if (frameName == WebView.MainFrameName) {
                    status = LoadStatus.Ready;
                }
                // start javascript execution engine on the component module
                if (FrameToComponentMap.TryGetValue(frameName, out var component)) {
                    if (component.Engine is ExecutionEngine engine) {
                        engine.Start();
                    }
                }
            }
        }

        private void OnReadyUIHandler(object[] args) {
            Ready?.Invoke();
        }

        private void OnWebViewJavascriptContextReleased(string frameName) {
            lock (SyncRoot) {
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

        /// <summary>
        /// Load the specified component into the main frame.
        /// </summary>
        /// <param name="component"></param>
        public void LoadComponent(IViewModule component) {
            LoadComponent(component, WebView.MainFrameName);
        }

        /// <summary>
        /// Load the specified component into the specified frame.
        /// </summary>
        public void LoadComponent(IViewModule component, string frameName) {
            lock (SyncRoot) {
                FrameToComponentMap[frameName] = component;
                BindModule(component, frameName);
                if (frameName == WebView.MainFrameName && status >= LoadStatus.PageLoaded) {
                    Load(component, frameName, loadComponentOnly: true);
                }
            }
        }

        /// <summary>
        /// Load the stylesheet, plugins and component (in that order).
        /// </summary>
        /// <param name="component"></param>
        /// <param name="frameName"></param>
        /// <param name="loadComponentOnly">If True, only the component will be loaded. Assumes that setylsheet and plugins were loaded previously.</param>
        private void Load(IViewModule component, string frameName, bool loadComponentOnly) {
            var plugins = GetPlugins(frameName);

            if (!loadComponentOnly) {
                if (frameName == WebView.MainFrameName) {
                    // only need to load the stylesheet for the main frame
                    if (DefaultStyleSheet != null) {
                        Loader.LoadDefaultStyleSheet(DefaultStyleSheet);
                    }
                }

                if (plugins.Length > 0) {
                    foreach (var module in plugins) {
                        RegisterNativeObject(module, frameName);
                    }

                    Loader.LoadPlugins(plugins, frameName);
                }
            }

            if (component != null) {
                if (frameName == WebView.MainFrameName) {
                    status = LoadStatus.ComponentLoading;
                }

                RegisterNativeObject(component, frameName);

                Loader.LoadComponent(component, frameName, DefaultStyleSheet != null, plugins.Length > 0);
            }
        }

        /// <summary>
        /// Page was loaded, load the component.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="frameName"></param>
        private void OnWebViewNavigated(string url, string frameName) {
            if (!url.StartsWith(ResourceUrl.EmbeddedScheme + Uri.SchemeDelimiter)) {
                // not a component, maybe its an iframe with an external url, bail out
                return;
            }

            lock (SyncRoot) {
                if (frameName == WebView.MainFrameName) {
                    status = LoadStatus.PageLoaded;
                }

                FrameToComponentMap.TryGetValue(frameName, out var component);
                Load(component, frameName, loadComponentOnly: false);
            }
        }

        /// <summary>
        /// An inner view was loaded, load its component.
        /// </summary>
        /// <param name="args"></param>
        private void OnViewInitialized(params object[] args) {
            var frameName = (string)args.FirstOrDefault();
            lock (SyncRoot) {
                FrameToComponentMap.TryGetValue(frameName, out var component);
                Load(component, frameName, loadComponentOnly: false);
            }
        }

        /// <summary>
        /// An inner view was destroyed, cleanup its resources.
        /// </summary>
        /// <param name="args"></param>
        private void OnViewDestroyed(params object[] args) {
            var frameName = (string)args.FirstOrDefault();
            lock (SyncRoot) {
                if (FrameToExecutionEngineMap.TryGetValue(frameName, out var executionEngine)) {
                    FrameToExecutionEngineMap.Remove(frameName);
                }
                if (FrameToComponentMap.TryGetValue(frameName, out var component)) {
                    FrameToComponentMap.Remove(frameName);
                }
                if (FrameToPluginsMap.TryGetValue(frameName, out var plugins)) {
                    FrameToPluginsMap.Remove(frameName);
                }
            }
        }

        /// <summary>
        /// Gets or sets the url of the default stylesheet.
        /// </summary>
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

            lock (SyncRoot) {
                FrameToPluginsMap[frameName] = GetPlugins(frameName).Concat(plugins).ToArray();

                foreach (var plugin in plugins) {
                    BindModule(plugin, frameName);
                }
            }
        }

        /// <summary>
        /// Binds a module with the spcified frame. When a module is bound to a frame, it will execute its methods on the frame instance.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="frameName"></param>
        internal void BindModule(IViewModule module, string frameName) {
            ExecutionEngine engine;
            lock (SyncRoot) {
                if (!FrameToExecutionEngineMap.TryGetValue(frameName, out engine)) {
                    engine = new ExecutionEngine(WebView, frameName);
                    FrameToExecutionEngineMap[frameName] = engine;
                }
            }
            module.Bind(engine);
        }

        /// <summary>
        /// Retrieves the specified plugin module instance for the spcifies frame.
        /// </summary>
        /// <typeparam name="T">Type of the plugin to retrieve.</typeparam>
        /// <param name="frameName"></param>
        /// <exception cref="InvalidOperationException">If the plugin hasn't been registered on the specified frame.</exception>
        /// <returns></returns>
        public T WithPlugin<T>(string frameName = WebView.MainFrameName) {
            var plugin = GetPlugins(frameName).OfType<T>().FirstOrDefault();
            if (plugin == null) {
                throw new InvalidOperationException($"Plugin {typeof(T).Name} not found in {frameName}");
            }
            return plugin;
        }

        /// <summary>
        /// Opens the developer tools.
        /// </summary>
        public void ShowDeveloperTools() {
            WebView.ShowDeveloperTools();
        }

        /// <summary>
        /// Closes the developer tools.
        /// </summary>
        public void CloseDeveloperTools() {
            WebView.CloseDeveloperTools();
        }

        /// <summary>
        /// Starts watching for sources changes, reloading the webview when a change occurs.
        /// </summary>
        /// <param name="mainModuleFullPath"></param>
        /// <param name="mainModuleResourcePath"></param>
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

        /// <summary>
        /// Handles the webview load of resources
        /// </summary>
        /// <param name="resourceHandler"></param>
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

        /// <summary>
        /// Registers a .net object to be available on the js context.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="frameName"></param>
        private void RegisterNativeObject(IViewModule module, string frameName) {
            WebView.RegisterJavascriptObject(module.GetNativeObjectFullName(frameName), module.CreateNativeObject(), executeCallsInUI: false);
        }

        /// <summary>
        /// Get the plugins modules for the specified frame.
        /// </summary>
        /// <param name="frameName"></param>
        /// <returns></returns>
        private IViewModule[] GetPlugins(string frameName) {
            FrameToPluginsMap.TryGetValue(frameName, out var plugins);
            return plugins ?? new IViewModule[0];
        }

        /// <summary>
        /// Converts an url to a full path url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string ToFullUrl(string url) {
            if (url.Contains(Uri.SchemeDelimiter)) {
                return url;
            } else if (url.StartsWith(ResourceUrl.PathSeparator)) {
                return new ResourceUrl(ResourceUrl.EmbeddedScheme, url).ToString();
            } else {
                return new ResourceUrl(UserCallingAssembly, url).ToString();
            }
        }

        /// <summary>
        /// Normalizes the url path separators
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string NormalizeUrl(string url) {
            return url.Replace("\\", ResourceUrl.PathSeparator);
        }
    }
}
