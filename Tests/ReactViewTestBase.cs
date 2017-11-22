using System;
using WebViewControl;

namespace Tests {
    public class ReactViewTestBase : TestBase<ReactView> {

        protected override void InitializeView() {
            var initialized = false;
            TargetView.Ready += () => initialized = true;
            TargetView.Source = "ReactViewResources/TestApp";
            WaitFor(() => initialized, TimeSpan.FromSeconds(10), "view initialized");
        }

    }
}
