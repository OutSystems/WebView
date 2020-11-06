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

            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}