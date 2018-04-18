using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WebViewControl {

    internal static class CefSubProcessWatchDog {

        public static void StartWatching(string browserSubProcessPath, bool debug) {
            Task.Run(() => {
                var cefSubprocessName = Path.GetFileName(browserSubProcessPath);
                var processStartInfo = new ProcessStartInfo("WebViewWatcher.exe", $"{cefSubprocessName} {(debug ? "1" : "0")}") {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(processStartInfo);
            });
        }
    }
}
