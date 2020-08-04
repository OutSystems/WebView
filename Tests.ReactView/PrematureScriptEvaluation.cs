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

        [Test(Description = "Test executing a method before view is queued until view is loaded")]
        public async Task ExecuteBeforeReady() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var scriptExecuted = false;
                TargetView.Event += delegate {
                    scriptExecuted = true;
                    taskCompletionSource.SetResult(true);
                };
                TargetView.ExecuteMethod("callEvent");
                Assert.IsFalse(TargetView.IsReady);
                Assert.IsFalse(scriptExecuted);

                Window.Show();
                await taskCompletionSource.Task;

                Assert.IsTrue(scriptExecuted);                
            });
        }
    }
}
