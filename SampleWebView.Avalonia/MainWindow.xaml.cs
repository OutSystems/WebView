using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal class MainWindow : Window {

        public MainWindow() {
            WebView.OsrEnabled = false;
            AvaloniaXamlLoader.Load(this);

            DataContext = new MainWindowViewModel();
        }
    }
}