using System;

namespace Tests {

    public abstract class ReactViewTestBase : TestBase<TestReactView> {

        protected override void InitializeView() {
            WaitFor(() => TargetView.IsReady, TimeSpan.FromSeconds(10), "view initialized");
        }

        protected override bool ReuseView => false;
    }
}
