using System;
using System.Threading.Tasks;
using NLog.Targets;
using NUnit.Framework;

namespace Tests.ReactView {

    public class PrematureScriptEvaluation : ReactViewTestBase {

        protected override bool AwaitReady => false;

        protected override void InitializeView() { 
            //Do nothing on purpose. Window is shown explicitly
        }

        [Test(Description = "Tests that an execution of a method is queued until view is loaded (window is shown)")]
        public async Task ExecuteBeforeReady() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                TargetView.Event += delegate {
                    taskCompletionSource.SetResult(true);
                };
                TargetView.ExecuteMethod("callEvent");
                Assert.IsFalse(TargetView.IsReady);
                Assert.IsFalse(taskCompletionSource.Task.IsCompleted);

                Window.Show();
                var isReady = await taskCompletionSource.Task;

                Assert.IsTrue(isReady);
            });
        }
    }
}
