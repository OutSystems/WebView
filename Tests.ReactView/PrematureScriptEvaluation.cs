using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.ReactView {

    public class PrematureScriptEvaluation : ReactViewTestBase {

        protected override bool AwaitReady => false;

        [Test(Description = "Test executing a method before view is ready")]
        public async Task ExecuteBeforeReady() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var isViewReady = false;
                TargetView.Event += delegate { isViewReady = TargetView.IsReady; taskCompletionSource.SetResult(true); };
                TargetView.ExecuteMethod("callEvent");
                await taskCompletionSource.Task;
                Assert.IsFalse(isViewReady);
                Assert.IsTrue(taskCompletionSource.Task.Result, "Event was not called!");
            });
        }
    }
}
