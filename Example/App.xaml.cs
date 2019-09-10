using System.Windows;
using WebViewControl;

namespace Example {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public App() {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
        }

        protected override void OnStartup(StartupEventArgs e) {
            WebView.LogFile = "ceflog.txt";
            base.OnStartup(e);
        }
    }
}
