﻿using System;
using NUnit.Framework;

namespace Tests.ReactView {

    [Ignore("Needs browser setup")]
    public class PrematureScriptEvaluation : ReactViewTestBase {

        protected override bool WaitForReady => false;

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
