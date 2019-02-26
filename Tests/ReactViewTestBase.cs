using System;

namespace Tests {

    public abstract class ReactViewTestBase<T> : TestBase<T> where T : TestReactView, new() {

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
            if (WaitForReady) {
                WaitFor(() => TargetView.IsReady, TimeSpan.FromSeconds(10), "view initialized");
            }
        }

        protected virtual bool WaitForReady => true;

        protected override bool ReuseView => false;

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

    public class ReactViewTestBase : ReactViewTestBase<TestReactView> { }
}
