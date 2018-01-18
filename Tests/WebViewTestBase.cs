using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class WebViewTestBase : TestBase<WebView> {

        protected bool FailOnAsyncExceptions { get; set; } = true;

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
            LoadAndWaitReady("<html><script>;</script><body>Test page</body></html>", TimeSpan.FromSeconds(10), "webview initialization");
        }

        private void OnUnhandledAsyncException(Exception e) {
            if (FailOnAsyncExceptions) {
                Assert.Fail("An async exception ocurred: " + e.Message);
            }
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
