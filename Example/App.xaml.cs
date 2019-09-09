using System.Windows;
using WebViewControl;

namespace Example {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        protected override void OnStartup(StartupEventArgs e) {
            WebView.LogFile = "ceflog.txt";
            base.OnStartup(e);
        }
    }
}
