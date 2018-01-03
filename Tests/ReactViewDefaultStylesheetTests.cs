using System;
using NUnit.Framework;

namespace Tests {

    public class ReactViewDefaultStyleSheetTests : ReactViewTestBase {

        protected override void InitializeView() {
            TargetView.DefaultStyleSheet = "ReactViewResources/css/default.css";
            base.InitializeView();
        }

        [Test(Description = "Tests default stylesheets get loaded")]
        public void DefaultStylesheetIsLoaded() {
            string stylesheet = null;
            TargetView.Event += (args) => {
                stylesheet = args;
            };

            TargetView.ExecuteMethodOnRoot("checkStyleSheetLoaded");

            WaitFor(() => stylesheet != null, TimeSpan.FromSeconds(10), "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".bar"));
        }
    }
}
