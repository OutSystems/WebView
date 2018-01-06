using System;

namespace Tests {

    public abstract class ReactViewTestBase : TestBase<TestReactView> {

        protected override void InitializeView() {
            var initialized = false;
            TargetView.Ready += () => initialized = true;
            WaitFor(() => initialized, TimeSpan.FromSeconds(10), "view initialized");
        }

        protected override bool ReuseView => false;
    }
}
