using ReactViewControl;
using System;
using System.Windows;
using WebViewControl;

namespace Example {
    /// <summary>
    /// Interaction logic for ReactViewExample.xaml
    /// </summary>
    public partial class ReactViewExample : Window {

        public ReactViewExample() {
            InitializeComponent();

            var subView = new SubExampleViewModule();
            subView.ConstantMessage = "This is a sub view";
            subView.GetTime += OnSubViewGetTime;
            exampleView.AttachInnerView(subView, "test");
            subView.CallMe();
        }

        private void OnExampleViewClick(SomeType arg) {
            MessageBox.Show("Clicked on a button inside the React view", ".Net Says");
        }

        private void OnWPFButtonClick(object sender, RoutedEventArgs e) {
            exampleView.CallMe();
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
    }
}
