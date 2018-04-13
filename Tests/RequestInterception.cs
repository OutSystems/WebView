using System;
using System.IO;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class RequestInterception : WebViewTestBase {

        private const string ResourceJs = "resource.js";
        private static readonly string HtmlWithResource = $"<html><script src='{ResourceJs}' onerror='scriptFailed = true'></script><body>Test Page</body></html>";

        protected override bool ReuseView {
            get { return false; }
        }

        private static Stream ToStream(string str) {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Test(Description = "Resource requested is intercepted")]
        public void ResourceRequestIsIntercepted() {
            var resourceRequested = "";
            TargetView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => resourceRequested = resourceHandler.Url;
            LoadAndWaitReady(HtmlWithResource);

            Assert.AreEqual("/" + ResourceJs, new Uri(resourceRequested).AbsolutePath);
        }

        [Test(Description = "Resource response with a stream is loaded properly")]
        public void InterceptedResourceIsLoaded() {
            TargetView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => resourceHandler.RespondWith(ToStream("scriptLoaded = true"), "js"); // declare x
            LoadAndWaitReady(HtmlWithResource);

            var loaded = TargetView.EvaluateScript<bool>("scriptLoaded"); // check that the value of x is what was declared before in the resource
            Assert.True(loaded);
        }

        [Test(Description = "Resource request canceled is not loaded")]
        public void ResourceRequestIsCanceled() {
            TargetView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => resourceHandler.Cancel();
            LoadAndWaitReady(HtmlWithResource);

            var failed = TargetView.EvaluateScript<bool>("scriptFailed"); // check that the value of x is what was declared before in the resource
            Assert.True(failed);
        }

        [Test(Description = "Resource request is redirected")]
        public void ResourceRedirect() {
            var redirected = false;
            const string RedirectUrl = "anotherResource.js";

            TargetView.BeforeResourceLoad += (WebView.ResourceHandler resourceHandler) => {
                var url = new Uri(resourceHandler.Url);
                switch (url.AbsolutePath.TrimStart('/')) {
                    case ResourceJs:
                        resourceHandler.Redirect(url.GetLeftPart(UriPartial.Authority) + "/" + RedirectUrl);
                        break;
                    case RedirectUrl:
                        redirected = true;
                        break;
                }
            };
            LoadAndWaitReady(HtmlWithResource);

            Assert.True(redirected);
        }

    }
}
