using Avalonia;
using Avalonia.Markup.Xaml;

namespace Tests {

    public class App : Application {

        public App() { }

        public override void Initialize() {
            WebViewControl.WebView.Settings.OsrEnabled = false;
            AvaloniaXamlLoader.Load(this);
        }
    }
}
