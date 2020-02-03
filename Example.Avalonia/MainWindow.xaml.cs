using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WebViewControl;

namespace Example.Avalonia {

    internal class MainWindow : Window {

        private ExampleView view;
        private SubExampleViewModule childView;

        private const string InnerViewName = "test";
        private int childViewCounter;

        public MainWindow() {
            WebView.OsrEnabled = false;
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            var browserWrapper = this.FindControl<Decorator>("browserWrapper");

            view = new ExampleView();
            view.Click += OnExampleViewClick;
            view.GetTime += OnExampleViewGetTime;
            view.ConstantMessage = "This is an example";
            view.Image = ImageKind.Beach;
            view.ViewMounted += OnViewMounted;

            view.WithPlugin<ViewPlugin>().NotifyViewLoaded += OnNotifyViewLoaded;

            view.CustomResourceRequested  += OnViewResourceRequested;

            browserWrapper.Child = view;
        }

        private void OnExampleViewClick(SomeType arg) {
            AppendLog("Clicked on a button inside the React view");
        }

        private void OnCallMainViewMenuItemClick(object sender, RoutedEventArgs e) {
            view.CallMe();
        }

        private void OnCallInnerViewMenuItemClick(object sender, RoutedEventArgs e) {
            childView.CallMe();
        }

        private void OnCallInnerViewPluginMenuItemClick(object sender, RoutedEventArgs e) {
            childView.WithPlugin<ViewPlugin>().Test();
        }

        private void OnShowDevTools(object sender, RoutedEventArgs e) {
            view.ShowDeveloperTools();
        }

        private string OnExampleViewGetTime() {
            return DateTime.Now.ToShortTimeString();
        }

        private void OnNotifyViewLoaded(string viewName) {
            AppendLog("On view loaded: " + viewName);
        }

        private void OnViewMounted() {
            var subViewId = childViewCounter++;
            childView = (SubExampleViewModule) view.SubView;
            childView.ConstantMessage = "This is a sub view";
            childView.GetTime += () => DateTime.Now.AddHours(1).ToShortTimeString() + $"(Id: {subViewId})";
            childView.CustomResourceRequested += OnInnerViewResourceRequested;
            childView.WithPlugin<ViewPlugin>().NotifyViewLoaded += (viewName) => AppendLog($"On sub view loaded (Id: {subViewId}): {viewName}");
            childView.CallMe();
            childView.Load();
        }

        private void AppendLog(string log) {
            Dispatcher.UIThread.Post(() => {
                var status = this.FindControl<TextBox>("status");
                status.Text = log + Environment.NewLine + status.Text;
            });
        }

        private Stream OnViewResourceRequested(string resourceKey, params string[] options) {
            return ResourcesManager.TryGetResource(GetType().Assembly, new[] { "ExampleView", "ExampleView", resourceKey });
        }

        private Stream OnInnerViewResourceRequested(string resourceKey, params string[] options) {
            return ResourcesManager.GetResource(GetType().Assembly, new[] { "ExampleView", "SubExampleView", resourceKey });
        }
    }
}