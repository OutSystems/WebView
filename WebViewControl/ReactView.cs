using System;
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
#if DEBUG
            windows = windows.Where(w => w.GetType().FullName != "Microsoft.VisualStudio.DesignTools.WpfTap.WpfVisualTreeService.Adorners.AdornerLayerWindow");
#endif
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
                cachedView = new ReactViewRender();
                if (window == null) {
                    window = new Window() {
                        ShowActivated = false,
                        WindowStyle = WindowStyle.None,
                        ShowInTaskbar = false,
                        Visibility = Visibility.Hidden,
                        Width = 1000,
                        Height = 1000,
                        Top = int.MinValue,
                        Left = int.MinValue,
                        IsEnabled = false
                    };
                    window.Show();
                }
                window.Content = cachedView;
            }), DispatcherPriority.Background);
            return result ?? new ReactViewRender();
        }

        private readonly ReactViewRender view;

        public ReactView(bool usePreloadedWebView = true) {
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited

            if (usePreloadedWebView) {
                view = CreateReactViewInstance();
            } else {
                view = new ReactViewRender();
            }
            Content = view;
            view.LoadComponent(Source, CreateRootPropertiesObject());
        }

        public static readonly DependencyProperty DefaultStyleSheetProperty = DependencyProperty.Register(
            nameof(DefaultStyleSheet),
            typeof(string),
            typeof(ReactView), 
            new PropertyMetadata(OnDefaultStyleSheetPropertyChanged));

        private static void OnDefaultStyleSheetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((ReactView)d).view.DefaultStyleSheet = (string) e.NewValue;
        }

        public string DefaultStyleSheet {
            get { return (string)GetValue(DefaultStyleSheetProperty); }
            set { SetValue(DefaultStyleSheetProperty, value); }
        }

        public static readonly DependencyProperty AdditionalModuleProperty = DependencyProperty.Register(
            nameof(AdditionalModule),
            typeof(string),
            typeof(ReactView),
            new PropertyMetadata(OnAdditionalModulePropertyChanged));

        private static void OnAdditionalModulePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((ReactView)d).view.AdditionalModule = (string) e.NewValue;
        }

        public string AdditionalModule {
            get { return (string)GetValue(AdditionalModuleProperty); }
            set { SetValue(AdditionalModuleProperty, value); }
        }

        public bool EnableDebugMode { get => view.EnableDebugMode; set => view.EnableDebugMode = value; }

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

        protected virtual object CreateRootPropertiesObject() {
            return null;
        }

        protected virtual string Source => null;

        protected void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            view.ExecuteMethodOnRoot(methodCall, args);
        }

        protected T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return view.EvaluateMethodOnRoot<T>(methodCall, args);
        }
    }
}
