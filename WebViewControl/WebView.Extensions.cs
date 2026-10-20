using System.Linq;

namespace WebViewControl {

    internal static partial class WebViewExtensions {

        public static string[] GetFrameNames(this WebView webview) {
            return webview.Engine?.GetFrameNames().Where(n => !webview.IsMainFrame(n)).ToArray() ?? new string[0];
        }

        internal static bool HasFrame(this WebView webview, string name) {
            return webview.GetFrame(name) != null;
        }

        internal static IJavascriptFrame GetFrame(this WebView webview, string frameName) {
            return webview.Engine?.GetFrame(frameName ?? "");
        }

        internal static bool IsMainFrame(this WebView webview, string frameName) {
            return string.IsNullOrEmpty(frameName);
        }
    }
}
