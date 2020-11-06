using Avalonia;
using Avalonia.ReactiveUI;

namespace SampleWebView.Avalonia {

    class Program {
        static void Main(string[] args) {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .UseReactiveUI()
                      .StartWithClassicDesktopLifetime(args);
        }
    }
}
