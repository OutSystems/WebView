using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WebViewControl {

    public partial class ReactView : ContentControl, IReactView {

        private static Window window;
        private static ReactViewRender cachedView;

        static ReactView() {
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(OnWindowLoaded), true);
        }

        private static void OnWindowLoaded(object sender, EventArgs e) {
            var window = (Window)sender;
            window.Closed -= OnWindowLoaded;
            window.Closed += OnWindowClosed;
        }

        private static void OnWindowClosed(object sender, EventArgs e) {
            var windows = Application.Current.Windows.Cast<Window>();
            if (Debugger.IsAttached) {
                windows = windows.Where(w => w.GetType().FullName != "Microsoft.VisualStudio.DesignTools.WpfTap.WpfVisualTreeService.Adorners.AdornerLayerWindow");
            }
            if (windows.Count() == 1 && windows.Single() == window) {
                // close helper window
                window.Close();
                window = null;
            }
        }

        private static ReactViewRender CreateReactViewInstance() {
            var result = cachedView;
            cachedView = null;
            Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                if (cachedView == null && !Application.Current.Dispatcher.HasShutdownStarted) {
                    cachedView = new ReactViewRender();
                    if (window == null) {
                        window = new Window() {
                            ShowActivated = false,
                            WindowStyle = WindowStyle.None,
                            ShowInTaskbar = false,
                            Visibility = Visibility.Hidden,
                            Width = 50,
                            Height = 50,
                            Top = int.MinValue,
                            Left = int.MinValue,
                            IsEnabled = false,
                            Title = "ReactViewRender Background Window"
                        };
                        window.Show();
                    }
                    window.Content = cachedView;
                }
            }), DispatcherPriority.Background);
            return result ?? new ReactViewRender();
        }

        private readonly ReactViewRender view;

        public ReactView(bool usePreloadedWebView = true) {
            if (usePreloadedWebView) {
                view = CreateReactViewInstance();
            } else {
                view = new ReactViewRender();
            }
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited, must be called after view is created otherwise view might be null
            Content = view;
            Dispatcher.BeginInvoke(DispatcherPriority.Send, (Action)(() => {
                if (EnableHotReload) {
                    view.EnableHotReload(Source);
                }
                view.LoadComponent(JavascriptSource, JavascriptName, CreateNativeObject());
            }));
        }

        public ResourceUrl DefaultStyleSheet { get => view.DefaultStyleSheet; set => view.DefaultStyleSheet = value; }

        public IViewModule[] Plugins { get => view.Plugins; set => view.Plugins = value; }

        public Dictionary<string, ResourceUrl> Mappings { get => view.Mappings; set => view.Mappings = value; }

        public bool EnableDebugMode { get => view.EnableDebugMode; set => view.EnableDebugMode = value; }

        public bool EnableHotReload { get; set; }

        public bool IsReady => view.IsReady;

        public event Action Ready {
            add { view.Ready += value; }
            remove { view.Ready -= value; }
        }

        public event Action<UnhandledExceptionEventArgs> UnhandledAsyncException {
            add { view.UnhandledAsyncException += value; }
            remove { view.UnhandledAsyncException -= value; }
        }

        public void Dispose() {
            view.Dispose();
        }

        protected virtual string JavascriptSource => null;

        protected virtual string JavascriptName => null;

        protected virtual string Source => null; // used for hot reload

        protected virtual object CreateNativeObject() {
            return null;
        }

        protected void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            view.ExecuteMethodOnRoot(methodCall, args);
        }

        protected T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return view.EvaluateMethodOnRoot<T>(methodCall, args);
        }

        public void ShowDeveloperTools() {
            view.ShowDeveloperTools();
        }

        public void CloseDeveloperTools() {
            view.CloseDeveloperTools();
        }
    }
}
