using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal partial class MainWindowV1 : Window {

        public MainWindowV1() {
            WebView.Settings.LogFile = "ceflog.txt";
            AvaloniaXamlLoader.Load(this);

            DataContext = new MainWindowV1ViewModel(this.FindControl<WebView>("webview"));
        }
    }
}