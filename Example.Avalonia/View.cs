using System;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using WebViewControl;

namespace Example.Avalonia {

    internal class View : ContentControl, IStyleable {

        Type IStyleable.StyleKey => typeof(ContentControl);

        private ExampleView view;
        private SubExampleViewModule childView;

        private int childViewCounter;

        public View() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            view = new ExampleView();
            view.Click += OnExampleViewClick;
            view.GetTime += OnExampleViewGetTime;
            view.ConstantMessage = "This is an example";
            view.Image = ImageKind.Beach;
            view.ViewMounted += OnViewMounted;

            view.WithPlugin<ViewPlugin>().NotifyViewLoaded += OnNotifyViewLoaded;

            view.CustomResourceRequested  += OnViewResourceRequested;

            Content = view;
        }

        private void OnExampleViewClick(SomeType arg) {
            Thread.Sleep(3000);
            AppendLog("Clicked on a button inside the React view");
        }

        public void CallMainViewMenuItemClick() {
            view.CallMe();
        }

        public void CallInnerViewMenuItemClick() {
            childView.CallMe();
        }

        public void CallInnerViewPluginMenuItemClick() {
            childView.WithPlugin<ViewPlugin>().Test();
        }

        public void ShowDevTools() {
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
                status.Text = DateTime.Now + ": " + log + Environment.NewLine + status.Text;
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