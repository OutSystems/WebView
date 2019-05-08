using System;
using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests {

    public class ReactViewDefaultStyleSheetLoadTests : ReactViewTestBase {

        protected class ViewFactoryWithStyleSheet : TestReactViewFactory {
            public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(ReactViewDefaultStyleSheetLoadTests).Assembly, "ReactViewResources", "Test", "default.css");
        }

        protected class ReactViewWithStyleSheet : TestReactView {
            protected override ReactViewFactory Factory => new ViewFactoryWithStyleSheet();
        }

        protected override TestReactView CreateView() {
            return new ReactViewWithStyleSheet();
        }

        [Test(Description = "Tests default stylesheets get loaded")]
        public void DefaultStylesheetIsLoaded() {
            string stylesheet = null;
            TargetView.Event += (args) => {
                stylesheet = args;
            };

            TargetView.ExecuteMethodOnRoot("checkStyleSheetLoaded", "2");

            WaitFor(() => stylesheet != null, DefaultTimeout, "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".bar"));
        }
    }
}
