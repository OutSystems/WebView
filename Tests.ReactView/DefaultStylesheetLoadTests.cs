using System.Threading.Tasks;
using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests.ReactView {

    public class DefaultStyleSheetLoadTests : ReactViewTestBase {

        protected class ViewFactoryWithStyleSheet : TestReactViewFactory {
            public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "Generated", "Default.css");
        }

        protected class ReactViewWithStyleSheet : TestReactView {
            protected override ReactViewFactory Factory => new ViewFactoryWithStyleSheet();
        }

        protected override TestReactView CreateView() {
            return new ReactViewWithStyleSheet();
        }

        [Test(Description = "Tests default stylesheets get loaded")]
        public async Task DefaultStylesheetIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<string>();

                TargetView.Event += (args) => {
                    taskCompletionSource.SetResult(args);
                };

                TargetView.ExecuteMethod("checkStyleSheetLoaded", "2");
                var stylesheet = await taskCompletionSource.Task;

                Assert.IsTrue(stylesheet.Contains(".bar"));
            });
        }
    }
}
