using Avalonia;
using Avalonia.ReactiveUI;

namespace SampleWebView.Avalonia {

    class Program {
        static void Main(string[] args) {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .With(new Win32PlatformOptions { UseWindowsUIComposition = false })
                      .UseReactiveUI()
                      .StartWithClassicDesktopLifetime(args);
        }
    }
}
