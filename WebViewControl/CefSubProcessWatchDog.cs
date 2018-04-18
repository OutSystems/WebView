using System.Diagnostics;
using System.IO;

namespace WebViewControl {

    internal static class CefSubProcessWatchDog {

        public static void StartWatching(string browserSubProcessPath, bool debug) {
            var cefSubprocessName = Path.GetFileName(browserSubProcessPath);
            var processStartInfo = new ProcessStartInfo("WebViewWatcher.exe", $"{cefSubprocessName} {(debug ? "1" : "0")}") {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(processStartInfo);
        }
    }
}
