using System.IO;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    [Ignore("Needs browser setup")]
    public class ResourcesLoading : WebViewTestBase {

        [Test(Description = "Html load encoding is well handled")]
        public void HtmlIsWellEncoded() {
            const string BodyContent = "some text and a double byte char '●'";
            var navigated = false;
            TargetView.Navigated += (_, __) => navigated = true;

            TargetView.LoadHtml($"<html><script>;</script><body>{BodyContent}</body></html>");
            WaitFor(() => navigated);
            var body = TargetView.EvaluateScript<string>("document.body.innerText");

            Assert.AreEqual(BodyContent, body);
        }

        [Test(Description = "Embedded files are correctly loaded")]
        public void EmbeddedFilesLoad() {
            var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "EmbeddedJavascriptFile.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetView.EvaluateScript<bool>("embeddedFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);
        }

        [Test(Description = "Embedded files with dashes in the filename are correctly loaded")]
        public void EmbeddedFilesWithDashesInFilenameLoad() {
            var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "dash-folder", "EmbeddedJavascriptFile-With-Dashes.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetView.EvaluateScript<bool>("embeddedFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);
        }

        [Test(Description = "WPF resource files are loaded")]
        public void ResourceFile() {
            var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "ResourceJavascriptFile.js");
            LoadAndWaitReady($"<html><script src='{embeddedResourceUrl}'></script></html>");
            var embeddedFileLoaded = TargetView.EvaluateScript<bool>("resourceFileLoaded");
            Assert.IsTrue(embeddedFileLoaded);

            Stream missingResource = null;
            Assert.DoesNotThrow(() => missingResource = ResourcesManager.TryGetResourceWithFullPath(GetType().Assembly, new[] { "Resources", "Missing.txt" }));
            Assert.IsNull(missingResource);
        }
    }
}
