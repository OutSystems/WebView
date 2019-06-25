using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WebViewControl;

namespace ReactViewControl {

    public delegate void ResourceRequestedEventHandler(WebView.ResourceHandler resourceHandler);

    public delegate Stream CustomResourceRequestedEventHandler(string url);

    public abstract class ReactView : UserControl, IDisposable {

        private static Dictionary<Type, ReactViewRender> CachedViews { get; } = new Dictionary<Type, ReactViewRender>();

        private ReactViewRender View { get; }

        private static ReactViewRender CreateReactViewInstance(ReactViewFactory factory) {
            ReactViewRender InnerCreateView() {
                var view = new ReactViewRender(factory.DefaultStyleSheet, factory.Plugins, factory.EnableViewPreload, factory.EnableDebugMode);
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

            View.BindModule(mainModule, WebView.MainFrameName);
            MainModule = mainModule;

            IsVisibleChanged += OnIsVisibleChanged;

            Content = View;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, View.FocusableElement);
        }

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
                    View.EnableHotReload(MainModule.Source, MainModule.JavascriptSource);
                }
                View.LoadComponent(MainModule);
            }
        }

        ~ReactView() {
            Dispose();
        }

        public void Dispose() {
            View.Dispose();
            GC.SuppressFinalize(this);
        }

        public T WithPlugin<T>() {
            return View.WithPlugin<T>();
        }

        protected void AddMappings(params SimpleViewModule[] mappings) {
            View.Plugins = View.Plugins.Concat(mappings).ToArray();
        }

        public bool EnableDebugMode { get => View.EnableDebugMode; set => View.EnableDebugMode = value; }

        public bool EnableHotReload { get; set; }

        public bool IsReady => View.IsReady;

        public double ZoomPercentage { get => View.ZoomPercentage; set => View.ZoomPercentage = value; }

        public event Action Ready {
            add { View.Ready += value; }
            remove { View.Ready -= value; }
        }

        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { View.UnhandledAsyncException += value; }
            remove { View.UnhandledAsyncException -= value; }
        }

        public event ResourceLoadFailedEventHandler ResourceLoadFailed {
            add { View.ResourceLoadFailed += value; }
            remove { View.ResourceLoadFailed -= value; }
        }

        public event ResourceRequestedEventHandler EmbeddedResourceRequested {
            add { View.EmbeddedResourceRequested += value; }
            remove { View.EmbeddedResourceRequested -= value; }
        }

        public event CustomResourceRequestedEventHandler CustomResourceRequested {
            add { View.CustomResourceRequested += value; }
            remove { View.CustomResourceRequested -= value; }
        }

        public event ResourceRequestedEventHandler ExternalResourceRequested {
            add { View.ExternalResourceRequested += value; }
            remove { View.ExternalResourceRequested -= value; }
        }

        public void ShowDeveloperTools() {
            View.ShowDeveloperTools();
        }

        public void CloseDeveloperTools() {
            View.CloseDeveloperTools();
        }
        
        protected IViewModule MainModule { get; }

        /// <summary>
        /// Number of preloaded views that are mantained in cache for each view.
        /// Components with different property values are stored in different cache entries.
        /// Defaults to 6. 
        /// </summary>
        public static int PreloadedCacheEntriesSize { get; set; } = 6;

        public void AttachInnerView(IViewModule viewModule, string frameName) {
            View.LoadComponent(viewModule, frameName);
        }
    }
}