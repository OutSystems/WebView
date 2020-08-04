using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.ReactView {

    public class InnerViewTests : ReactViewTestBase {

        protected override void InitializeView() {
            TargetView.AutoShowInnerView = true;
            base.InitializeView();
        }

        [Test(Description = "Tests inner view load")]
        public async Task InnerViewIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var innerView = new InnerViewModule();
                innerView.Loaded += () => taskCompletionSource.SetResult(true);

                innerView.Load();
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Inner view module was not loaded!");
            });
        }

        [Test(Description = "Tests inner view load")]
        public async Task InnerViewMethodIsExecuted() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var innerView = new InnerViewModule();
                innerView.MethodCalled += () => taskCompletionSource.SetResult(true);

                innerView.Load();
                innerView.TestMethod();
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Method was not called!");
            });
        }
    }
}
