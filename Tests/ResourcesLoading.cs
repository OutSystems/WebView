using NUnit.Framework;

namespace Tests {

    public class ResourcesLoading : WebViewTestBase {

        [Test(Description = "Html load encoding is well handled")]
        public void HtmlIsWellEncoded() {
            const string BodyContent = "some text and a double byte char '●'";
            var navigated = false;
            TargetView.Navigated += _ => navigated = true;

            TargetView.LoadHtml($"<html><script>;</script><body>{BodyContent}</body></html>");
            WaitFor(() => navigated);
            var body = TargetView.EvaluateScript<string>("document.body.innerText");

            Assert.AreEqual(BodyContent, body);
        }

        [Test(Description = "Embedded files are correctly loaded")]
        public void EmbeddedFilesLoad() {
            var embeddedResourceUrl = WebViewControl.WebView.BuildEmbeddedResourceUrl(GetType().Assembly, "Tests", "Resources", "EmbeddedJavascriptFile.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetView.EvaluateScript<bool>("embeddedFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);
        }

        [Test(Description = "Embedded files with dashes in the filename are correctly loaded")]
        public void EmbeddedFilesWithDashesInFilenameLoad() {
            var embeddedResourceUrl = WebViewControl.WebView.BuildEmbeddedResourceUrl(GetType().Assembly, "Tests", "Resources", "dash-folder", "EmbeddedJavascriptFile-With-Dashes.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetView.EvaluateScript<bool>("embeddedFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);
        }
    }
}
