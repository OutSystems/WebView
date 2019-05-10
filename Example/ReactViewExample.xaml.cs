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

        private InitialState OnGetInitialState() {
            return new InitialState() {
                constantMessage = "This is an example"
            };
        }
    }
}
