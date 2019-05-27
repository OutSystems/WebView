using System;

namespace Tests.WebView {

    public class WebViewTestBase : TestBase<WebViewControl.WebView> {

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
        }

        protected override void AfterInitializeView() {
            base.AfterInitializeView();
            LoadAndWaitReady("<html><script>;</script><body>Test page</body></html>", TimeSpan.FromSeconds(10), "webview initialization");
        }

        protected void LoadAndWaitReady(string html) {
            LoadAndWaitReady(html, DefaultTimeout);
        }

        protected void LoadAndWaitReady(string html, TimeSpan timeout, string timeoutMsg = null) {
            var navigated = false;
            void OnNavigated(string url) {
                navigated = true;
            }
            try {
                TargetView.Navigated += OnNavigated;
                TargetView.LoadHtml(html);
                WaitFor(() => navigated, timeout, timeoutMsg);
            } finally {
                TargetView.Navigated -= OnNavigated;
            }
        }

        protected void WithUnhandledExceptionHandling(Action action, Func<Exception, bool> onException) {
            Action<WebViewControl.UnhandledAsyncExceptionEventArgs> unhandledException = (e) => {
                e.Handled = onException(e.Exception);
            };

            var failOnAsyncExceptions = FailOnAsyncExceptions;
            FailOnAsyncExceptions = false;
            TargetView.UnhandledAsyncException += unhandledException;

            try {
                action();
            } finally {
                TargetView.UnhandledAsyncException -= unhandledException;
                FailOnAsyncExceptions = failOnAsyncExceptions;
            }
        }
    }
}
