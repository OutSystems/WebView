using Avalonia;

namespace Example.Avalonia {
    class Program {
        static void Main(string[] args) {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .StartWithClassicDesktopLifetime(args);
        }
    }
}
