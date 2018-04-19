using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace WebViewControl {

    internal static class CefSubProcessWatchDog {

        public static void StartWatching(string browserSubProcessPath, bool debug) {
            Task.Run(() => {
                var watcherPath = new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase), "WebViewWatcher.exe")).LocalPath;
                var cefSubprocessName = Path.GetFileName(browserSubProcessPath);
                var processStartInfo = new ProcessStartInfo(watcherPath, $"{cefSubprocessName} {(debug ? "1" : "0")}") {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(processStartInfo);
            });
        }
    }
}
