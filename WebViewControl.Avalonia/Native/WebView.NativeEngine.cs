using System;

namespace WebViewControl {

    partial class WebView {

        private partial IWebViewEngine CreateEngine() {
            if (OperatingSystem.IsMacOS()) {
                return new WKWebViewEngine(this);
            }
            if (OperatingSystem.IsWindows()) {
                return new WebView2Engine(this);
            }
            throw new PlatformNotSupportedException("The native web engine is available on macOS (WKWebView) and Windows (WebView2). Use the CEF flavor of WebViewControl on other platforms.");
        }
    }
}
