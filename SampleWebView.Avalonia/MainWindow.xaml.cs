using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace SampleWebView.Avalonia
{

    internal class MainWindow : Window
    {

        public MainWindow()
        {
            WebView.OsrEnabled = false;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            var webView = new WebView();
            webView.Address = "http://www.google.com";

            var grid = this.FindControl<Grid>("grid");
            grid.Children.Add(webView);
        }
    }
}