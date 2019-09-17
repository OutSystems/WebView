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

        private object SyncRoot { get; } = new object();

        internal const string MainViewFrameName = "";
        internal const string ModulesObjectName = "__Modules__";
        
        private const string ViewInitializedEventName = "ViewInitialized";
        private const string ViewDestroyedEventName = "ViewDestroyed";
        private const string ViewLoadedEventName = "ViewLoaded";
        private const string CustomResourceBaseUrl = "resource";

        private static Assembly ResourcesAssembly { get; } = typeof(ReactViewResources.Resources).Assembly;

        private Dictionary<string, FrameInfo> Frames { get; } = new Dictionary<string, FrameInfo>();

        private WebView WebView { get; }
        private Assembly UserCallingAssembly { get; }
        private LoaderModule Loader { get; }
        private Func<IViewModule[]> PluginsFactory { get; }

        private bool enableDebugMode = false;
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
            AddPlugins(MainViewFrameName, initializePlugins());
            EnableDebugMode = enableDebugMode;

            var loadedListener = WebView.AttachListener(ViewLoadedEventName);
            loadedListener.Handler += OnViewLoaded;
            loadedListener.UIHandler += OnViewLoadedUIHandler;

            WebView.AttachListener(ViewInitializedEventName).Handler += OnViewInitialized;
            WebView.AttachListener(ViewDestroyedEventName).Handler += OnViewDestroyed;

            WebView.Disposed += OnWebViewDisposed;
            WebView.JavascriptContextReleased += OnWebViewJavascriptContextReleased;
            WebView.BeforeResourceLoad += OnWebViewBeforeResourceLoad;
            
            Content = WebView;

            var urlParams = new string[] {
                new ResourceUrl(ResourcesAssembly).ToString(),
                enableDebugMode ? "1" : "0",
                ModulesObjectName,
                Listener.EventListenerObjName,
                ViewInitializedEventName,
                ViewDestroyedEventName,
                ViewLoadedEventName,
                ResourceUrl.CustomScheme +  Uri.SchemeDelimiter + CustomResourceBaseUrl
            };

            WebView.LoadResource(new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.DefaultUrl + "?" + string.Join("&", urlParams)));
        }

        public IInputElement FocusableElement => WebView.FocusableElement;

        public bool IsDisposing => WebView.IsDisposing;

        /// <summary>
        /// True when the main component has been rendered.
        /// </summary>
        public bool IsReady => Frames.Any() && Frames[MainViewFrameName].LoadStatus == LoadStatus.Ready;

        /// <summary>
        /// True when view component is loading or loaded
        /// </summary>
        public bool IsMainComponentLoaded => Frames.Any() && Frames[MainViewFrameName].LoadStatus >= LoadStatus.ComponentLoading;

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
        /// Handle external resource requests. 
        /// Call <see cref="WebView.ResourceHandler.BeginAsyncResponse"/> to handle the request in an async way.
        /// </summary>
        public event ResourceRequestedEventHandler ExternalResourceRequested;

        /// <summary>
        /// An view was initialized, load its component.
        /// </summary>
        /// <param name="args"></param>
        private void OnViewInitialized(params object[] args) {
            var frameName = (string)args.FirstOrDefault();

            lock (SyncRoot) {
                var frame = GetOrCreateFrame(frameName);
                frame.LoadStatus = LoadStatus.ViewInitialized;
                frame.PluginsLoaded = false;
                Load(frame);
            }
        }

        /// <summary>
        /// Handle component loaded event: component is loaded and ready for interaction.
        /// </summary>
        /// <param name="args"></param>
        private void OnViewLoaded(params object[] args) {
            var frameName = (string)args.FirstOrDefault();

            lock (SyncRoot) {
                var frame = GetOrCreateFrame(frameName);
                frame.LoadStatus = LoadStatus.Ready;
                
                // start component execution engine
                if (frame.Component != null) {
                    if (frame.Component.Engine is ExecutionEngine engine) {
                        engine.Start();
                    }
                }
            }
        }

        /// <summary>
        /// An inner view was destroyed, cleanup its resources.
        /// </summary>
        /// <param name="args"></param>
        private void OnViewDestroyed(params object[] args) {
            var frameName = (string)args.FirstOrDefault();
            lock (SyncRoot) {
                if (Frames.TryGetValue(frameName, out var frame)) {
                    var modules = frame.Plugins ?? Enumerable.Empty<IViewModule>();
                    if (frame.Component != null) {
                        modules = modules.Concat(new[] { frame.Component });
                    }
                    foreach (var module in modules) {
                        UnregisterNativeObject(module, frame.Name);
                    }
                    Frames.Remove(frameName);
                }
            }
        }

        /// <summary>
        /// Javascript context was destroyed, cleanup everthing.
        /// </summary>
        /// <param name="frameName"></param>
        private void OnWebViewJavascriptContextReleased(string frameName) {
            if (frameName != MainViewFrameName) {
                return;
            }

            lock (SyncRoot) {
                var mainFrame = Frames[MainViewFrameName];
                Frames.Clear();
                Frames.Add(MainViewFrameName, mainFrame);
                mainFrame.LoadStatus = LoadStatus.Initialized;
                mainFrame.PluginsLoaded = false;
                mainFrame.ExecutionEngine = null;
            }
        }

        private void OnViewLoadedUIHandler(object[] args) {
            Ready?.Invoke();
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
            LoadComponent(component, MainViewFrameName);
        }

        /// <summary>
        /// Load the specified component into the specified frame.
        /// </summary>
        public void LoadComponent(IViewModule component, string frameName) {
            lock (SyncRoot) {
                var frame = GetOrCreateFrame(frameName);
                frame.Component = component;
                
                BindModule(component, frame);
                if (frame.LoadStatus == LoadStatus.ViewInitialized) {
                    Load(frame);
                }
            }
        }

        /// <summary>
        /// Load the stylesheet, plugins and component (in that order).
        /// </summary>
        /// <param name="frame"></param>
        private void Load(FrameInfo frame) {
            if (!frame.PluginsLoaded) {
                if (frame.Name == MainViewFrameName) {
                    // only need to load the stylesheet for the main frame
                    if (DefaultStyleSheet != null) {
                        Loader.LoadDefaultStyleSheet(DefaultStyleSheet);
                    }
                }

                if (frame.Plugins?.Length > 0) {
                    foreach (var module in frame.Plugins) {
                        RegisterNativeObject(module, frame.Name);
                    }

                    Loader.LoadPlugins(frame.Plugins, frame.Name);

                    frame.PluginsLoaded = true;
                }
            }

            if (frame.Component != null) {
                frame.LoadStatus =  LoadStatus.ComponentLoading;

                RegisterNativeObject(frame.Component, frame.Name);

                Loader.LoadComponent(frame.Component, frame.Name, DefaultStyleSheet != null, frame.Plugins?.Length > 0);
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
            var invalidPlugins = plugins.Where(p => string.IsNullOrEmpty(p.MainJsSource) || string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.NativeObjectName));
            if (invalidPlugins.Any()) {
                var pluginName = invalidPlugins.First().Name + "|" + invalidPlugins.First().GetType().Name;
                throw new ArgumentException($"Plugin '{pluginName}' is invalid");
            }

            lock (SyncRoot) {
                var frame = GetOrCreateFrame(frameName);

                if (frame.LoadStatus > LoadStatus.ViewInitialized) {
                    throw new InvalidOperationException($"Cannot add plugins after component has been loaded");
                }

                frame.Plugins = frame.Plugins != null ? frame.Plugins.Concat(plugins).ToArray() : plugins;

                foreach (var plugin in plugins) {
                    BindModule(plugin, frame);
                }
            }
        }

        /// <summary>
        /// Binds a module with the spcified frame. When a module is bound to a frame, it will execute its methods on the frame instance.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="frameName"></param>
        private void BindModule(IViewModule module, FrameInfo frame) {
            if (frame.ExecutionEngine == null) {
                frame.ExecutionEngine = new ExecutionEngine(WebView, frame.Name);
            }
            module.Bind(frame.ExecutionEngine);
        }

        /// <summary>
        /// Binds a module with the spcified frame. When a module is bound to a frame, it will execute its methods on the frame instance.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="frameName"></param>
        internal void BindModule(IViewModule module, string frameName) {
            lock(SyncRoot) {
                var frame = GetOrCreateFrame(frameName);
                BindModule(module, frame);
            }
        }

        /// <summary>
        /// Retrieves the specified plugin module instance for the spcifies frame.
        /// </summary>
        /// <typeparam name="T">Type of the plugin to retrieve.</typeparam>
        /// <param name="frameName"></param>
        /// <exception cref="InvalidOperationException">If the plugin hasn't been registered on the specified frame.</exception>
        /// <returns></returns>
        public T WithPlugin<T>(string frameName = MainViewFrameName) {
            if (!Frames.TryGetValue(frameName, out var frame)) {
                throw new InvalidOperationException($"Frame {frameName} is not loaded");
            }
            
            var plugin = frame.Plugins.OfType<T>().FirstOrDefault();
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
        /// Add an handler for custom resources from the specified frame.
        /// </summary>
        /// <param name="frameName"></param>
        /// <param name="handler"></param>
        public void AddCustomResourceRequestedHandler(string frameName, CustomResourceRequestedEventHandler handler) {
            var frame = GetOrCreateFrame(frameName);
            frame.CustomResourceRequested += handler;
        }

        /// <summary>
        /// Remve the handler for custom resources from the specified frame.
        /// </summary>
        /// <param name="frameName"></param>
        /// <param name="handler"></param>
        public void RemoveCustomResourceRequestedHandler(string frameName, CustomResourceRequestedEventHandler handler) {
            // do not create if frame does not exist
            if (Frames.TryGetValue(frameName, out var frame)) {
                frame.CustomResourceRequested -= handler;
            }
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
                    HandleCustomResourceRequested(resourceHandler);
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
        /// Handle custom resource request and forward it to the appropriate frame.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private void HandleCustomResourceRequested(WebView.ResourceHandler resourceHandler) {
            var url = resourceHandler.Url;
            
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Segments.Length > 1 && uri.Host.Equals(CustomResourceBaseUrl, StringComparison.InvariantCultureIgnoreCase)) {
                var frameName = uri.Segments.ElementAt(1).TrimEnd(ResourceUrl.PathSeparator.ToCharArray());
                if (frameName != null && Frames.TryGetValue(frameName, out var frame)) {
                    var customResourceRequestedHandlers = frame.CustomResourceRequested?.GetInvocationList().ToArray();
                    if (customResourceRequestedHandlers?.Any() == true) {
                        resourceHandler.BeginAsyncResponse(() => {
                            // get resource key from the query params
                            var resourceKey = uri.Query.TrimStart('?');
                            // get response from first handler that returns a stream
                            var response = customResourceRequestedHandlers.Cast<CustomResourceRequestedEventHandler>().Select(h => h(resourceKey)).FirstOrDefault(r => r != null);

                            if (response != null) {
                                var path = uri.AbsolutePath;
                                var extension = Path.GetExtension(path).TrimStart('.');
                                resourceHandler.RespondWith(response, extension);
                            } else {
                                resourceHandler.RespondWith(MemoryStream.Null);
                            }
                        });
                    }
                }
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
        /// Unregisters a .net object available on the js context.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="frameName"></param>
        private void UnregisterNativeObject(IViewModule module, string frameName) {
            WebView.UnregisterJavascriptObject(module.GetNativeObjectFullName(frameName));
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

        private FrameInfo GetOrCreateFrame(string frameName) {
            if (!Frames.TryGetValue(frameName, out var frame)) {
                frame = new FrameInfo(frameName);
                Frames[frameName] = frame;
            }
            return frame;
        }
    }
}
