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

                TargetView.Event += delegate {  
                    taskCompletionSource.SetResult(TargetView.IsReady); 
                };
                TargetView.ExecuteMethod("callEvent");
                await taskCompletionSource.Task;

                Assert.IsFalse(taskCompletionSource.Task.Result, "View should not be ready yet!");
            });
        }
    }
}
