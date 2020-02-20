using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Example.Avalonia {
    class Program {
        static void Main(string[] args) {
            var appBuilder = AppBuilder.Configure<App>().UsePlatformDetect();

            var lifetime = new ClassicDesktopStyleApplicationLifetime() {
                ShutdownMode = ShutdownMode.OnLastWindowClose
            };
            appBuilder.SetupWithLifetime(lifetime);

            var window = new MainWindow();

            window.Show();
            //window.CreateTab();

            var cts = new CancellationTokenSource();
            appBuilder.Instance.Run(cts.Token);
        }
    }
}
