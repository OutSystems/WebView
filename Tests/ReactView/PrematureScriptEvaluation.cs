using System;
using NUnit.Framework;

namespace Tests.ReactView {

    public class PrematureScriptEvaluation : ReactViewTestBase {

        protected override bool WaitForReady => false;

        [Test(Description = "Test properties are injected in react component and root object is exposed")]
        public void ExecuteBeforeReady() {
            var eventCalled = false;
            TargetView.Event += (args) => eventCalled = true;
            TargetView.ExecuteMethodOnRoot("callEvent");
            WaitFor(() => eventCalled, TimeSpan.FromSeconds(10), "event call");
        }
    }
}
