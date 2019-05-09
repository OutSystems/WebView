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

    public partial class ReactView : UserControl, IViewModule, IDisposable {

        private static readonly Dictionary<Type, ReactViewRender> cachedViews = new Dictionary<Type, ReactViewRender>();

        private readonly ReactViewRender view;

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
                if (cachedViews.TryGetValue(factoryType, out var cachedView)) {
                    cachedViews.Remove(factoryType);
                }

                // create a new view in the background and put it in the cache
                Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                    if (!cachedViews.ContainsKey(factoryType) && !Application.Current.Dispatcher.HasShutdownStarted) {
                        cachedViews.Add(factoryType, InnerCreateView());
                    }
                }), DispatcherPriority.Background);

                if (cachedView != null) {
                    return cachedView;
                }
            }

            return InnerCreateView();
        }

        public ReactView() : this(initialize: true) {
        }

        protected ReactView(bool initialize) {
            view = CreateReactViewInstance(Factory);
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited, must be called after view is created otherwise view might be null

            if (initialize) {
                Initialize();
            }

            Content = view;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, view.FocusableElement);
        }

        protected virtual ReactViewFactory Factory => new ReactViewFactory();

        protected void Initialize() {
            if (!view.IsComponentLoaded) {
                if (EnableHotReload) {
                    view.EnableHotReload(Source);
                }
                view.LoadComponent(this);
            }
        }

        ~ReactView() {
            Dispose();
        }

        public void Dispose() {
            view.Dispose();
            GC.SuppressFinalize(this);
        }

        public T WithPlugin<T>() {
            return view.WithPlugin<T>();
        }

        protected void AddMappings(params SimpleViewModule[] mappings) {
            view.Plugins = view.Plugins.Concat(mappings).ToArray();
        }

        public bool EnableDebugMode { get => view.EnableDebugMode; set => view.EnableDebugMode = value; }

        public bool EnableHotReload { get; set; }

        public bool IsReady => view.IsReady;

        public double ZoomPercentage { get => view.ZoomPercentage; set => view.ZoomPercentage = value; }

        public event Action Ready {
            add { view.Ready += value; }
            remove { view.Ready -= value; }
        }

        public event Action<UnhandledAsyncExceptionEventArgs> UnhandledAsyncException {
            add { view.UnhandledAsyncException += value; }
            remove { view.UnhandledAsyncException -= value; }
        }

        public event Action<string> ResourceLoadFailed {
            add { view.ResourceLoadFailed += value; }
            remove { view.ResourceLoadFailed -= value; }
        }

        public event Func<string, Stream> CustomResourceRequested {
            add { view.CustomResourceRequested += value; }
            remove { view.CustomResourceRequested -= value; }
        }

        public void ShowDeveloperTools() {
            view.ShowDeveloperTools();
        }

        public void CloseDeveloperTools() {
            view.CloseDeveloperTools();
        }

        string IViewModule.JavascriptSource => JavascriptSource;

        protected virtual string JavascriptSource => null;

        string IViewModule.NativeObjectName => NativeObjectName;

        protected virtual string NativeObjectName => null;

        protected virtual string ModuleName => null;

        string IViewModule.Name => ModuleName;

        string IViewModule.Source => Source;

        protected virtual string Source => null; // used for hot reload

        object IViewModule.CreateNativeObject() => CreateNativeObject();

        protected virtual object CreateNativeObject() {
            return null;
        }

        void IViewModule.Bind(IExecutionEngine engine) {
            throw new Exception("Cannot bind ReactView");
        }

        IExecutionEngine IViewModule.ExecutionEngine => ExecutionEngine;

        protected IExecutionEngine ExecutionEngine => view; // ease access in generated code

        public static bool UseEnhancedRenderingEngine { get; set; } = true;
    }
}