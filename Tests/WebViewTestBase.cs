using System;
using WebViewControl;

namespace Tests {

    public class WebViewTestBase : TestBase<WebView> {

        protected override void InitializeView() {
            LoadAndWaitReady("<html><script>;</script><body>Test page</body></html>", TimeSpan.FromSeconds(10), "webview initialization");
        }

        protected void LoadAndWaitReady(string html) {
            LoadAndWaitReady(html, DefaultTimeout);
        }

        protected void LoadAndWaitReady(string html, TimeSpan timeout, string timeoutMsg = null) {
            var navigated = false;
            TargetView.Navigated += (string url) => navigated = true;
            TargetView.LoadHtml(html);
            WaitFor(() => navigated, timeout, timeoutMsg);
        }
    }
}
