using NUnit.Framework;

namespace Tests {

    public class ResourcesLoading : TestBase {

        [Test(Description = "Html load encoding is well handled")]
        public void HtmlEncoding() {
            const string BodyContent = "some text and a double byte char '●'";
            var navigated = false;
            TargetWebView.Navigated += _ => navigated = true;
            
            TargetWebView.LoadHtml($"<html><script>;</script><body>{BodyContent}</body></html>");
            WaitFor(() => navigated, DefaultTimeout);
            var body = TargetWebView.EvaluateScript<string>("document.body.innerText");

            Assert.AreEqual(BodyContent, body);
        }

        [Test(Description = "Embedded files are correctly loaded")]
        public void EmbeddedFilesLoad() {
            var embeddedResourceUrl = WebViewControl.WebView.BuildEmbeddedResourceUrl(GetType().Assembly, "Tests", "Resources", "EmbeddedJavascriptFile.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetWebView.EvaluateScript<bool>("embeddedFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);
        }
    }
}
