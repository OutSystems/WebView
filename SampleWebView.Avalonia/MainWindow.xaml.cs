using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal class MainWindow : Window {

        public MainWindow() {
            WebView.Settings.OsrEnabled = false;
            WebView.Settings.LogFile = "ceflog.txt";
            AvaloniaXamlLoader.Load(this);

            DataContext = new MainWindowViewModel(this.FindControl<WebView>("webview"));
        }
    }
}