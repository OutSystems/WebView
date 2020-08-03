using System;
using NUnit.Framework;

namespace Tests.ReactView {

    public class PrematureScriptEvaluation : ReactViewTestBase {

        protected override bool AwaitReady => false;

        [Test(Description = "Test executing a method before view is ready")]
        public void ExecuteBeforeReady() {
            var eventCalled = false;
            TargetView.Event += (args) => eventCalled = true;
            TargetView.ExecuteMethod("callEvent");
            Assert.IsFalse(TargetView.IsReady);
            WaitFor(() => eventCalled, DefaultTimeout, "event call");
        }
    }
}
