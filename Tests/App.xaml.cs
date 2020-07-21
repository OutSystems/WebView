using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Tests {

    public class App : Application {


        public static new App Current => (App)Application.Current;

        public App() {
            RegisterServices();
        }

        public override void Initialize() {
            WebViewControl.WebView.OsrEnabled = false;
            AvaloniaXamlLoader.Load(this);
        }

        public override void RegisterServices() {
            base.RegisterServices();
            AvaloniaLocator.CurrentMutable.Bind<IWindowingPlatform>();
        }

    
    }
}
