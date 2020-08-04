using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.ReactView {

    public class InnerViewTests : ReactViewTestBase {

        protected override void InitializeView() {
            if (TargetView != null) {
                TargetView.AutoShowInnerView = true;
            }
            base.InitializeView();
        }

        [Test(Description = "Tests inner view load")]
        public async Task InnerViewIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.InnerView.Loaded += () => taskCompletionSource.SetResult(true);
                TargetView.InnerView.Load();
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Inner view module was not loaded!");
            });
        }

        [Test(Description = "Tests inner view load")]
        public async Task InnerViewMethodIsExecuted() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var innerView = TargetView.InnerView;

                innerView.MethodCalled += () => taskCompletionSource.SetResult(true);
                innerView.Load();
                innerView.TestMethod();
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Method was not called!");
            });
        }
    }
}
