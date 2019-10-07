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

            view.AddCustomResourceRequestedHandler(OnViewResourceRequested);
            view.AddCustomResourceRequestedHandler(InnerViewName, OnInnerViewResourceRequested);

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
            view.WithPlugin<ViewPlugin>(InnerViewName).Test();
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
            childView = new SubExampleViewModule();
            childView.ConstantMessage = "This is a sub view";
            childView.GetTime += () => DateTime.Now.AddHours(1).ToShortTimeString() + $"(Id: {subViewId})";
            view.AttachInnerView(childView, InnerViewName);
            view.WithPlugin<ViewPlugin>(InnerViewName).NotifyViewLoaded += (viewName) => AppendLog($"On sub view loaded (Id: {subViewId}): {viewName}");
            childView.CallMe();
        }

        private void AppendLog(string log) {
            var status = this.FindControl<TextBox>("status");
            Dispatcher.UIThread.Post(() => status.Text = log + Environment.NewLine + status.Text);
        }

        private Stream OnViewResourceRequested(string resourceKey, params string[] options) {
            return ResourcesManager.GetResource(GetType().Assembly, new[] { "ExampleView", "ExampleView", resourceKey });
        }

        private Stream OnInnerViewResourceRequested(string resourceKey, params string[] options) {
            return ResourcesManager.GetResource(GetType().Assembly, new[] { "ExampleView", "SubExampleView", resourceKey });
        }
    }
}