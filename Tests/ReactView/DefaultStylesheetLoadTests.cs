using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests.ReactView {

    [Ignore("Needs browser setup")]
    public class DefaultStyleSheetLoadTests : ReactViewTestBase {

        protected class ViewFactoryWithStyleSheet : TestReactViewFactory {
            public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "ReactViewResources", "Test", "default.css");
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

            TargetView.ExecuteMethod("checkStyleSheetLoaded", "2");

            WaitFor(() => stylesheet != null, DefaultTimeout, "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".bar"));
        }
    }
}
