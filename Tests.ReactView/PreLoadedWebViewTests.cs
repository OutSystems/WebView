using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ReactViewControl;

namespace Tests.ReactView {

    public class TestReactViewFactoryWithPreload : TestReactViewFactory {
        public override bool EnableViewPreload => true;
    }

    public class TestReactViewWithPreload : TestReactView {

        public TestReactViewWithPreload() {
            AutoShowInnerView = true;
            var innerView = new InnerViewModule();
            innerView.Load();
        }

        protected override ReactViewFactory Factory => new TestReactViewFactoryWithPreload();
    }

    public class PreLoadedWebViewTests : ReactViewTestBase<TestReactViewWithPreload> {

        [Test(Description = "Loading a view with a inner view and preload enabled loads the component successfully the second time")]
        public async Task PreloadLoadsComponent() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                using var newView = new TestReactViewWithPreload();
                newView.Ready += delegate {
                    taskCompletionSource.SetResult(newView.IsReady);
                };
                Window.Content = newView;

                var isNewViewReady = await taskCompletionSource.Task;
                Assert.IsTrue(isNewViewReady, "Second view was not properly loaded!");
            });
        }

        [Test(Description = "Loading a view with preload enabled uses a webview from cache")]
        public async Task PreloadUsesWebViewFromCache() {
            await Run(async () => {
                var start = DateTime.Now;
                while ((DateTime.Now - start).TotalSeconds < 1) {
                    Thread.Sleep(1); // let the cached webview have time to be created
                }

                var taskCompletionSource = new TaskCompletionSource<bool>();

                var currentTime = TargetView.EvaluateMethod<double>("getCurrentTime");
                using var newView = new TestReactViewWithPreload();
                newView.Ready += delegate {
                    taskCompletionSource.SetResult(newView.IsReady);
                };
                Window.Content = newView;

                var isNewViewReady = await taskCompletionSource.Task;
                Assert.IsTrue(isNewViewReady, "Second view was not properly loaded!");

                var startTime = newView.EvaluateMethod<double>("getStartTime");
                Assert.LessOrEqual(startTime, currentTime, "The second webview should have been loaded before!");
            });
        }
    }
}
