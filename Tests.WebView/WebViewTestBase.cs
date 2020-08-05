using System;
using System.Threading.Tasks;
using WebViewControl;

namespace Tests.WebView {

    public class WebViewTestBase : TestBase<WebViewControl.WebView> {

        protected override void InitializeView() {
            if (TargetView != null) {
                TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
            }
            base.InitializeView();
        }

        protected override async Task AfterInitializeView() {
            await base.AfterInitializeView();

            var taskCompletionSource = new TaskCompletionSource<bool>();
            TargetView.WebViewInitialized += () => {
                taskCompletionSource.SetResult(true);
            };

            await taskCompletionSource.Task;
            await Load("<html><script>;</script><body>Test page</body></html>");
        }

        protected Task Load(string html) {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            
            void OnNavigated(string url, string frameName) {
                if (url != UrlHelper.AboutBlankUrl) {
                    try {
                        taskCompletionSource.SetResult(true);
                    } finally {
                        TargetView.Navigated -= OnNavigated;
                    }
                }
            }
            TargetView.Navigated += OnNavigated;
            TargetView.LoadHtml(html);
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
