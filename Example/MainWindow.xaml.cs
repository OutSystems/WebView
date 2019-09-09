using System.Windows;

namespace Example {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void OnShowWebViewClick(object sender, RoutedEventArgs e) {
            new WebViewExample().Show();
        }

        private void OnShowReactViewClick(object sender, RoutedEventArgs e) {
            new ReactViewExample().Show();
        }
    }
}
