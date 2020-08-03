using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.WebView {

    public class RequestInterception : WebViewTestBase {

        private const string ResourceJs = "resource.js";

        private static string HtmlWithResource { get; } = $"<html><script src='{ResourceJs}' onerror='scriptFailed = true'></script><body>Test Page</body></html>";

        private static Stream ToStream(string str) {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Test(Description = "Resource requested is intercepted")]
        public async Task ResourceRequestIsIntercepted() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var resourceRequested = "";
                TargetView.BeforeResourceLoad += (resourceHandler) => {
                    resourceRequested = resourceHandler.Url;
                    taskCompletionSource.SetResult(true);
                };
                await Load(HtmlWithResource);
                Task.WaitAll(taskCompletionSource.Task);

                Assert.AreEqual("/" + ResourceJs, new Uri(resourceRequested).AbsolutePath);
            });
        }

        [Test(Description = "Resource response with a stream is loaded properly")]
        public async Task InterceptedResourceRequestIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.BeforeResourceLoad += (resourceHandler) => {
                    resourceHandler.RespondWith(ToStream("scriptLoaded = true"), "js"); // declare x
                    taskCompletionSource.SetResult(true);
                };
                await Load(HtmlWithResource);
                Task.WaitAll(taskCompletionSource.Task);

                var loaded = TargetView.EvaluateScript<bool>("scriptLoaded"); // check that the value of x is what was declared before in the resource
                Assert.True(loaded);
            });
        }

        [Test(Description = "Resource request canceled is not loaded")]
        public async Task ResourceRequestIsCanceled() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.BeforeResourceLoad += (resourceHandler) => {
                    resourceHandler.Cancel();
                    taskCompletionSource.SetResult(true);
                };
                await Load(HtmlWithResource);
                Task.WaitAll(taskCompletionSource.Task);

                var failed = TargetView.EvaluateScript<bool>("scriptFailed"); // check that the value of x is what was declared before in the resource
                Assert.True(failed);
            });
        }

        [Test(Description = "Resource request is redirected")]
        public async Task RequestRedirect() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var redirected = false;
                const string RedirectUrl = "anotherResource.js";

                TargetView.BeforeResourceLoad += (resourceHandler) => {
                    var url = new Uri(resourceHandler.Url);
                    switch (url.AbsolutePath.TrimStart('/')) {
                        case ResourceJs:
                            resourceHandler.Redirect(url.GetLeftPart(UriPartial.Authority) + "/" + RedirectUrl);
                            break;
                        case RedirectUrl:
                            redirected = true;
                            taskCompletionSource.SetResult(true);
                            break;
                    }
                };
                await Load(HtmlWithResource);
                Task.WaitAll(taskCompletionSource.Task);

                Assert.True(redirected);
            });
        }
    }
}
