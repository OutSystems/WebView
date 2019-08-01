using System;
using System.Windows;

namespace Example {
    /// <summary>
    /// Interaction logic for ReactViewExample.xaml
    /// </summary>
    public partial class ReactViewExample : Window {

        private const string InnerViewName = "test";

        private SubExampleViewModule subView;

        public ReactViewExample() {
            InitializeComponent();

            subView = new SubExampleViewModule();
            subView.ConstantMessage = "This is a sub view";
            subView.GetTime += OnSubViewGetTime;
            exampleView.AttachInnerView(subView, InnerViewName);
            subView.CallMe();

            exampleView.WithPlugin<ViewPlugin>().NotifyViewLoaded += OnNotifyViewLoaded;
            exampleView.WithPlugin<ViewPlugin>(InnerViewName).NotifyViewLoaded += OnSubViewNotifyViewLoaded;
        }

        private void OnExampleViewClick(SomeType arg) {
            MessageBox.Show("Clicked on a button inside the React view", ".Net Says");
        }

        private void OnCallMainViewMenuItemClick(object sender, RoutedEventArgs e) {
            exampleView.CallMe();
        }

        private void OnCallInnerViewMenuItemClick(object sender, RoutedEventArgs e) {
            subView.CallMe();
        }

        private void OnCallInnerViewPluginMenuItemClick(object sender, RoutedEventArgs e) {
            exampleView.WithPlugin<ViewPlugin>(InnerViewName).Test();
        }

        private void OnShowDevTools(object sender, RoutedEventArgs e) {
            exampleView.ShowDeveloperTools();
        }

        private string OnExampleViewGetTime() {
            return DateTime.Now.ToShortTimeString();
        }

        private string OnSubViewGetTime() {
            return DateTime.Now.AddHours(1).ToShortTimeString();
        }

        private void OnNotifyViewLoaded(string viewName) {
            Console.WriteLine("On view loaded: " + viewName);
        }

        private void OnSubViewNotifyViewLoaded(string viewName) {
            Console.WriteLine("On sub view loaded: " + viewName);
        }
    }
}
