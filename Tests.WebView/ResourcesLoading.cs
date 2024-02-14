using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    public class ResourcesLoading : WebViewTestBase {

        [Test(Description = "Html load encoding is well handled")]
        public async Task HtmlIsWellEncoded() {
            await Run(async () => {
                const string BodyContent = "some text and a double byte char '●'";
                await Load($"<html><script>;</script><body>{BodyContent}</body></html>");

                var body = await TargetView.EvaluateScript<string>("return document.body.innerText");
                Assert.AreEqual(BodyContent, body);
            });
        }

        [Test(Description = "Embedded files are correctly loaded")]
        public async Task EmbeddedFilesLoad() {
            await Run(async () => {
                var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "EmbeddedJavascriptFile.js");
                await Load($"<html><script src='{embeddedResourceUrl}'></script></html>");

                var embeddedFileLoaded = await TargetView.EvaluateScript<bool>("return embeddedFileLoaded");
                Assert.IsTrue(embeddedFileLoaded);
            });
        }

        [Test(Description = "Embedded files with dashes in the filename are correctly loaded")]
        public async Task EmbeddedFilesWithDashesInFilenameLoad() {
            await Run(async () => {
                var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "dash-folder", "EmbeddedJavascriptFile-With-Dashes.js");
                await Load($"<html><script src='{embeddedResourceUrl}'></script></html>");

                var embeddedFileLoaded = await TargetView.EvaluateScript<bool>("return embeddedFileLoaded");
                Assert.IsTrue(embeddedFileLoaded);
            });
        }

        [Test(Description = "Avalonia resource files are loaded")]
        public async Task ResourceFile() {
            await Run(async () => {
                var embeddedResourceUrl = new ResourceUrl(GetType().Assembly, "Resources", "ResourceJavascriptFile.js");
                await Load($"<html><script src='{embeddedResourceUrl}'></script></html>");

                var resourceFileLoaded = await TargetView.EvaluateScript<bool>("return resourceFileLoaded");
                Assert.IsTrue(resourceFileLoaded);

                Stream missingResource = null;
                Assert.DoesNotThrow(() => missingResource = ResourcesManager.TryGetResourceWithFullPath(GetType().Assembly, new[] { "Resources", "Missing.txt" }));
                Assert.IsNull(missingResource);
            });
        }

        [Test(Description = "Resources from dynamically loaded assemblies can be loaded and the correct version is fetched")]
        public void DynamicallyLoadedAssemblyFile() {
            var resourcesAssemblyName = "TestResourceAssembly";

            ResourceUrl GetResourceUrl(Version version) {
                var executingAssembly = Assembly.GetExecutingAssembly();
                var executingDdirectory = Path.GetDirectoryName(executingAssembly.Location);
                var dllDirectory = executingDdirectory.Replace(executingAssembly.GetName().Name, $"{resourcesAssemblyName}.V{version}");
                var dllPath = Path.Combine(dllDirectory, $"{resourcesAssemblyName}.dll");
                var assembly = Assembly.Load(File.ReadAllBytes(dllPath));
                return new ResourceUrl(assembly, "Resource.txt");
            }

            string GetExpectedContent(Version version) => $"Resource with V{version} content";

            string GetResourceContent(Uri uri) {
                Stream resourceStream = null;
                Assert.DoesNotThrow(() => resourceStream = ResourcesManager.TryGetResource(uri));
                Assert.IsNotNull(resourceStream);

                using var reader = new StreamReader(resourceStream);
                return reader.ReadToEnd();
            }

            var version1 = new Version(1, 0, 0, 0);
            var version2 = new Version(2, 0, 0, 0);
            var versionsToTry = new[] { version1, version2 };
            foreach (var version in versionsToTry) {
                var uri = new Uri(GetResourceUrl(version).ToString());
                var content = GetResourceContent(uri);
                Assert.That(content, Is.EqualTo(GetExpectedContent(version)));
            }

            // check that we are also able to retrieve the resource without specifying a version,
            // but in that case the resource may be from either version
            var unversionedUri = new Uri($"embedded://webview/{resourcesAssemblyName}/Resource.txt");
            var unversionedContent = GetResourceContent(unversionedUri);
            Assert.That(unversionedContent, Is.AnyOf(GetExpectedContent(version1), GetExpectedContent(version2)));
        }
    }
}
