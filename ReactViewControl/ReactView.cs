using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WebViewControl;

namespace ReactViewControl {

    public delegate void ResourceRequestedEventHandler(ResourceHandler resourceHandler);

    public delegate Stream CustomResourceRequestedEventHandler(string url);

    public delegate Stream CustomResourceWithKeyRequestedEventHandler(string resourceKey);

    public abstract class ReactView : UserControl, IDisposable {

        private static Dictionary<Type, ReactViewRender> CachedViews { get; } = new Dictionary<Type, ReactViewRender>();

        private ReactViewRender View { get; }

        private static ReactViewRender CreateReactViewInstance(ReactViewFactory factory) {
            ReactViewRender InnerCreateView() {
                var view = new ReactViewRender(factory.DefaultStyleSheet, () => factory.InitializePlugins(), factory.EnableViewPreload, factory.EnableDebugMode);
                if (factory.ShowDeveloperTools) {
                    view.ShowDeveloperTools();
                }
                return view;
            }

            if (factory.EnableViewPreload) {
                var factoryType = factory.GetType();
                // check if we have a view cached for the current factory
                if (CachedViews.TryGetValue(factoryType, out var cachedView)) {
                    CachedViews.Remove(factoryType);
                }

                // create a new view in the background and put it in the cache
                Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() => {
                    if (!CachedViews.ContainsKey(factoryType) && !Dispatcher.CurrentDispatcher.HasShutdownStarted) {
                        CachedViews.Add(factoryType, InnerCreateView());
                    }
                }), DispatcherPriority.Background);

                if (cachedView != null) {
                    return cachedView;
                }
            }

            return InnerCreateView();
        }

        protected ReactView(IViewModule mainModule) {
            View = CreateReactViewInstance(Factory);
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited, must be called after view is created otherwise view might be null

            View.BindModule(mainModule, ReactViewRender.MainViewFrameName);
            MainModule = mainModule;

            IsVisibleChanged += OnIsVisibleChanged;

            Content = View;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, View.FocusableElement);
        }

        ~ReactView() {
            Dispose();
        }

        public void Dispose() {
            View.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Factory used to configure the initial properties of the control.
        /// </summary>
        protected virtual ReactViewFactory Factory => new ReactViewFactory();

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            // IWindowService is a WPF internal property set when component is loaded into a new window, even if the window isn't shown
            if (e.Property.Name == "IWindowService") {
                if (e.OldValue is Window oldWindow) {
                    oldWindow.IsVisibleChanged -= OnWindowIsVisibleChanged;
                }

                if (e.NewValue is Window newWindow) {
                    newWindow.IsVisibleChanged += OnWindowIsVisibleChanged;
                }
            }
        }

        private void OnWindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var window = (Window)sender;
            // this is the first event that we have available with guarantees that all the component properties have been set
            // since its not supposed to change properties once the component has been shown
            if (window.IsVisible) {
                window.IsVisibleChanged -= OnWindowIsVisibleChanged;
                LoadComponent();
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            // fallback when window was already shown
            if (IsVisible) {
                IsVisibleChanged -= OnIsVisibleChanged;
                LoadComponent();
            }
        }

        private void LoadComponent() {
            if (!View.IsMainComponentLoaded) {
                if (EnableHotReload) {
                    View.EnableHotReload(MainModule.Source, MainModule.MainJsSource);
                }
                View.LoadComponent(MainModule);
            }
        }

        /// <summary>
        /// Retrieves the specified plugin module instance for the spcifies frame.
        /// </summary>
        /// <typeparam name="T">Type of the plugin to retrieve.</typeparam>
        /// <param name="frameName"></param>
        /// <exception cref="InvalidOperationException">If the plugin hasn't been registered on the specified frame.</exception>
        /// <returns></returns>
        public T WithPlugin<T>(string frameName = ReactViewRender.MainViewFrameName) {
            return View.WithPlugin<T>(frameName);
        }

        /// <summary>
        /// Enables or disables debug mode. 
        /// In debug mode the webview developer tools becomes available pressing F12 and the webview shows an error message at the top with the error details 
        /// when a resource fails to load.
        /// </summary>
        public bool EnableDebugMode { get => View.EnableDebugMode; set => View.EnableDebugMode = value; }

        public bool EnableHotReload { get; set; }

        /// <summary>
        /// True when the main component has been rendered.
        /// </summary>
        public bool IsReady => View.IsReady;

        /// <summary>
        /// Gets or sets the control zoom percentage (1 = 100%)
        /// </summary>
        public double ZoomPercentage { get => View.ZoomPercentage; set => View.ZoomPercentage = value; }

        /// <summary>
        /// Event fired when the component is rendered and ready for interaction.
        /// </summary>
        public event Action Ready {
            add { View.Ready += value; }
            remove { View.Ready -= value; }
        }

        /// <summary>
        /// Event fired when an async exception occurs (eg: executing javascript)
        /// </summary>
        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { View.UnhandledAsyncException += value; }
            remove { View.UnhandledAsyncException -= value; }
        }

        /// <summary>
        /// Event fired when a resource fails to load.
        /// </summary>
        public event ResourceLoadFailedEventHandler ResourceLoadFailed {
            add { View.ResourceLoadFailed += value; }
            remove { View.ResourceLoadFailed -= value; }
        }

        /// <summary>
        /// Handle embedded resource requests. You can use this event to change the resource being loaded.
        /// </summary>
        public event ResourceRequestedEventHandler EmbeddedResourceRequested {
            add { View.EmbeddedResourceRequested += value; }
            remove { View.EmbeddedResourceRequested -= value; }
        }

        /// <summary>
        /// Handle custom resource requests. Use this event to load the resource based on the url.
        /// </summary>
        public event CustomResourceRequestedEventHandler CustomResourceRequested {
            add { View.CustomResourceRequested += value; }
            remove { View.CustomResourceRequested -= value; }
        }

        /// <summary>
        /// Add an handler for custom resources from main frame.
        /// </summary>
        /// <param name="handler"></param>
        public void AddCustomResourceRequestedHandler(CustomResourceWithKeyRequestedEventHandler handler) {
            AddCustomResourceRequestedHandler(ReactViewRender.MainViewFrameName, handler);
        }

        /// <summary>
        /// Add an handler for custom resources from the specified frame.
        /// </summary>
        /// <param name="frameName"></param>
        /// <param name="handler"></param>
        public void AddCustomResourceRequestedHandler(string frameName, CustomResourceWithKeyRequestedEventHandler handler) {
            View.AddCustomResourceRequestedHandler(frameName, handler);
        }

        /// <summary>
        /// Remve the handler for custom resources from the main frame.
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveCustomResourceRequestedHandler(CustomResourceWithKeyRequestedEventHandler handler) {
            RemoveCustomResourceRequestedHandler(ReactViewRender.MainViewFrameName, handler);
        }

        /// <summary>
        /// Remve the handler for custom resources from the specified frame.
        /// </summary>
        /// <param name="frameName"></param>
        /// <param name="handler"></param>
        public void RemoveCustomResourceRequestedHandler(string frameName, CustomResourceWithKeyRequestedEventHandler handler) {
            View.RemoveCustomResourceRequestedHandler(frameName, handler);
        }

        /// <summary>
        /// Handle external resource requests. 
        /// Call <see cref="WebView.ResourceHandler.BeginAsyncResponse"/> to handle the request in an async way.
        /// </summary>
        public event ResourceRequestedEventHandler ExternalResourceRequested {
            add { View.ExternalResourceRequested += value; }
            remove { View.ExternalResourceRequested -= value; }
        }

        /// <summary>
        /// Opens the developer tools.
        /// </summary>
        public void ShowDeveloperTools() {
            View.ShowDeveloperTools();
        }

        /// <summary>
        /// Closes the developer tools.
        /// </summary>
        public void CloseDeveloperTools() {
            View.CloseDeveloperTools();
        }
        
        /// <summary>
        /// View module of this control.
        /// </summary>
        protected IViewModule MainModule { get; }

        /// <summary>
        /// Number of preloaded views that are mantained in cache for each view.
        /// Components with different property values are stored in different cache entries.
        /// Defaults to 6. 
        /// </summary>
        public static int PreloadedCacheEntriesSize { get; set; } = 6;

        /// <summary>
        /// Loads the view module into the specified frame when the frame is rendered.
        /// </summary>
        /// <param name="viewModule"></param>
        /// <param name="frameName"></param>
        public void AttachInnerView(IViewModule viewModule, string frameName) {
            View.AddPlugins(frameName, Factory.InitializePlugins());
            View.LoadComponent(viewModule, frameName);
        }
    }
}