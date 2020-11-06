using System.Linq;
using Xilium.CefGlue;

namespace WebViewControl {

    internal static class WebViewExtensions {

        public static string[] GetFrameNames(this WebView webview) {
            return webview.GetCefBrowser()?.GetFrameNames().Where(n => !webview.IsMainFrame(n)).ToArray() ?? new string[0];
        }

        internal static bool HasFrame(this WebView webview, string name) {
            return webview.GetFrame(name) != null;
        }

        internal static CefFrame GetFrame(this WebView webview, string frameName) {
            return webview.GetCefBrowser()?.GetFrame(frameName ?? "");
        }

        internal static bool IsMainFrame(this WebView webview, string frameName) {
            return string.IsNullOrEmpty(frameName);
        }

        internal static void SendKeyEvent(this WebView webview, CefKeyEvent keyEvent) {
            webview.GetCefBrowser()?.GetHost()?.SendKeyEvent(keyEvent);
        }

        private static CefBrowser GetCefBrowser(this WebView webview) {
            return webview.UnderlyingBrowser.GetBrowser();
        }
    }
}
