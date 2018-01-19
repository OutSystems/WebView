using System;
using NUnit.Framework;

namespace Tests {

    public abstract class ReactViewTestBase : TestBase<TestReactView> {

        protected bool FailOnAsyncExceptions { get; set; } = true;

        protected override void InitializeView() {
            TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
            WaitFor(() => TargetView.IsReady, TimeSpan.FromSeconds(10), "view initialized");
        }

        private void OnUnhandledAsyncException(WebViewControl.UnhandledExceptionEventArgs e) {
            if (FailOnAsyncExceptions) {
                Assert.Fail("An async exception ocurred: " + e.Exception.Message);
            }
        }

        protected override bool ReuseView => false;
    }
}
