namespace WebViewControl {

    internal static class WebViewExtensions {

        internal static bool IsMainFrame(this WebView webview, string frameName) {
            return string.IsNullOrEmpty(frameName);
        }
    }
}
