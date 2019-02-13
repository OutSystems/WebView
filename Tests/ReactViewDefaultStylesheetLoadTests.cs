using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class ReactViewDefaultStyleSheetLoadTests : ReactViewTestBase {

        protected override void InitializeView() {
            TargetView.DefaultStyleSheet = new ResourceUrl(typeof(ReactViewDefaultStyleSheetLoadTests).Assembly, "ReactViewResources", "Test", "default.css");
            base.InitializeView();
        }

        [Test(Description = "Tests default stylesheets get loaded")]
        public void DefaultStylesheetIsLoaded() {
            string stylesheet = null;
            TargetView.Event += (args) => {
                stylesheet = args;
            };

            TargetView.ExecuteMethodOnRoot("checkStyleSheetLoaded", "2");

            WaitFor(() => stylesheet != null, TimeSpan.FromSeconds(10), "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".bar"));
        }
    }
}
