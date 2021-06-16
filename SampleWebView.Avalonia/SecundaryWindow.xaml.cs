using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal class SecundaryWindow : Window {
        public SecundaryWindow() {
            AvaloniaXamlLoader.Load(this);
            var webVieww = this.FindControl<WebView>("webview");
            DataContext = new MainWindowViewModel(webVieww);
        }

        public void FocusInner() {
            this.FindControl<WebView>("webview").Focus();
        }
    }
}