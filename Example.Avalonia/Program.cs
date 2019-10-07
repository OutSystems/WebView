using Avalonia;
using Xilium.CefGlue.Avalonia;

namespace Example.Avalonia {
    class Program {
        static void Main(string[] args) {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .UseSkia()
                      .ConfigureCefGlue(args)
                      .Start<MainWindow>();
        }
    }
}
