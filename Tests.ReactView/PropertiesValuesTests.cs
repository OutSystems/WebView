using System.Threading.Tasks;
using Avalonia.Controls;
using NUnit.Framework;

namespace Tests.ReactView {

    public class PropertiesValuesTests : ReactViewTestBase {

        protected override TestReactView CreateView() {
            return null;
        }

        protected override void ShowDebugConsole() { }

        [Test(Description = "Test setting properties after component added to window but window is not visible yet.")]
        public async Task PropertyValuesArePassedToView() {
            await Run(async () => {
                const string PropertyValue = "test value";
                using var sandbox = new Sandbox("initial value");
                var window = new Window() {
                    Title = CurrentTestName
                };

                try {
                    sandbox.AttachTo(window);
                    sandbox.PropertyValue = PropertyValue;
                    window.Show();
                    await sandbox.Initialize();

                    var actualPropertyValue = sandbox.GetPropertyValue();
                    Assert.AreEqual(PropertyValue, actualPropertyValue);
                } finally {
                    window.Close();
                }
            });
        }
    }
}
