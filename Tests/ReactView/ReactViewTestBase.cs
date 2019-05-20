using System;

namespace Tests.ReactView {

    public abstract class ReactViewTestBase<T> : TestBase<T> where T : TestReactView, new() {

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
        }

        protected override void AfterInitializeView() {
            if (WaitForReady) {
                WaitFor(() => TargetView.IsReady, DefaultTimeout, "view initialized");
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

    public class ReactViewTestBase : ReactViewTestBase<TestReactView> {

        protected override TestReactView CreateView() {
            TestReactView.PreloadedCacheEntriesSize = 0; // disable cache during tests
            return base.CreateView();
        }
    }
}
