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
                var isLoaded = await taskCompletionSource.Task;

                Assert.IsTrue(isLoaded, "Inner view module was not loaded!");
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
                var methodCalled = await taskCompletionSource.Task;

                Assert.IsTrue(methodCalled, "Method was not called!");
            });
        }
    }
}
