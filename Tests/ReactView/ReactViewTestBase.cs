using System;

namespace Tests.ReactView {

    public abstract class ReactViewTestBase<T> : TestBase<T> where T : TestReactView, new() {

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
        }

        // TODO
        //protected override void AfterInitializeView() {
        //    if (WaitForReady) {
        //        WaitFor(() => TargetView.IsReady, DefaultTimeout, "view initialized");
        //    }
        //}

        protected virtual bool WaitForReady => true;

        protected void WithUnhandledExceptionHandling(Action action, Func<Exception, bool> onException) {
            void OnUnhandledException(WebViewControl.UnhandledAsyncExceptionEventArgs e) {
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

    public class ReactViewTestBase : ReactViewTestBase<TestReactView> {

        protected override TestReactView CreateView() {
            TestReactView.PreloadedCacheEntriesSize = 0; // disable cache during tests
            return base.CreateView();
        }
    }
}
