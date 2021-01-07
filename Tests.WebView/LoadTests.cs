using System.Threading.Tasks;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    public class LoadTests : WebViewTestBase {

        protected override Task AfterInitializeView() => Task.CompletedTask;

        [Test(Description = "Custom schemes are loaded")]
        public async Task LoadCustomScheme() {
            await Run(async () => {
                var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "EmbeddedHtml.html");

                var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnNavigated(string url, string frameName) {
                    if (url != UrlHelper.AboutBlankUrl) {
                        TargetView.Navigated -= OnNavigated;
                        taskCompletionSource.SetResult(true);
                    }
                }
                TargetView.Navigated += OnNavigated;
                TargetView.LoadResource(embeddedResourceUrl);
                await taskCompletionSource.Task;

                var content = await TargetView.EvaluateScript<string>("document.documentElement.innerText");
                Assert.AreEqual("test", content);
            });
        }
    }
}
