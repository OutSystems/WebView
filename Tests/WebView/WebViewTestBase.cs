using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using WebViewControl;

namespace Tests.WebView {

    public class WebViewTestBase : TestBase<WebViewControl.WebView> {

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
        }

        protected override void AfterInitializeView() {
            base.AfterInitializeView();
            WaitFor(() => TargetView.IsBrowserInitialized, TimeSpan.FromSeconds(30), "browser initialization");
            LoadAndWaitReady("<html><script>;</script><body>Test page</body></html>", TimeSpan.FromSeconds(30), "webview initialization");
        }

        protected Task LoadAndWaitReady(string html) {
            return LoadAndWaitReady(html, DefaultTimeout);           
        }

        protected Task LoadAndWaitReady(string html, TimeSpan timeout, string timeoutMsg = null) {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            
            void OnNavigated(string url, string frameName) {
                taskCompletionSource.SetResult(true);
                TargetView.Navigated -= OnNavigated;
            }
            TargetView.Navigated += OnNavigated;
            TargetView.LoadHtml(html);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.MinValue);
            return taskCompletionSource.Task;
        }

        protected void WithUnhandledExceptionHandling(Action action, Func<Exception, bool> onException) {
            void OnUnhandledException(UnhandledAsyncExceptionEventArgs e) {
                e.Handled = onException(e.Exception);
            }

            var failOnAsyncExceptions = FailOnAsyncExceptions;
            FailOnAsyncExceptions = false;
            TargetView.UnhandledAsyncException += OnUnhandledException;

            try {
                action();
            } finally {
                TargetView.UnhandledAsyncException -= OnUnhandledException;
                FailOnAsyncExceptions = failOnAsyncExceptions;
            }
        }

        protected override void ShowDebugConsole() {
            TargetView.ShowDeveloperTools();
        }
    }
}
