using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class WebViewTestBase : TestBase<WebView> {

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
            TargetView.Navigated += (string url) => navigated = true;
            TargetView.LoadHtml(html);
            WaitFor(() => navigated, timeout, timeoutMsg);
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
