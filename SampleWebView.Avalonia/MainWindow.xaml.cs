using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal class MainWindow : Window {
        private static bool flag = true;

        public MainWindow() {
            if (flag) {
                flag = false;
                WebView.Settings.OsrEnabled = false;
                WebView.Settings.LogFile = "ceflog.txt";
            }
            
            AvaloniaXamlLoader.Load(this);
            DataContext = new MainWindowViewModel(this.FindControl<WebView>("webview"));

            var btn = this.FindControl<Button>("AmazingButton");
            btn.Click += BtnOnClick;
        }

        private void BtnOnClick(object sender, RoutedEventArgs e) {
            var newWindow = new MainWindow();
            newWindow.Show(this);
        }
    }
}