using System.Threading.Tasks;
using Avalonia.Controls;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    public class CommonTests : WebViewTestBase {

        [Test(Description = "Before navigate hook is called")]
        public async Task BeforeNavigateHookCalled() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.BeforeNavigate += (request) => {
                    taskCompletionSource.SetResult(true);
                    request.Cancel();
                };
                TargetView.Address = "https://www.google.com";
                var beforeNavigateCalled = await taskCompletionSource.Task;
                Assert.IsTrue(beforeNavigateCalled, "BeforeNavigate hook was not called!");
            });
        }

        [Test(Description = "Javascript evaluation on navigated event does not block")]
        public async Task JavascriptEvaluationOnNavigatedDoesNotBlock() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.Navigated += delegate {
                    TargetView.EvaluateScript<int>("1+1");
                    taskCompletionSource.SetResult(true);
                };
                await Load("<html><body></body></html>");
                var navigatedCalled = await taskCompletionSource.Task;
                Assert.IsTrue(navigatedCalled, "JS evaluation on navigated event is blocked!");
            });
        }

        [Test(Description = "Tests that the webview is disposed when host window is not shown")]
        public async Task WebViewIsDisposedWhenHostWindowIsNotShown() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var view = new WebViewControl.WebView();
                view.Disposed += delegate {
                    taskCompletionSource.SetResult(true);
                };

                var window = new Window { Title = CurrentTestName };

                try {
                    window.Content = view;
                    window.Close();

                    var disposed = await taskCompletionSource.Task;
                    Assert.IsTrue(disposed);
                } finally {
                    window.Close();
                }
            });
        }
    }
}
