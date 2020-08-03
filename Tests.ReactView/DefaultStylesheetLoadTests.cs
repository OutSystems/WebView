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
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var stylesheet = string.Empty;

                TargetView.Event += (args) => {
                    stylesheet = args;
                    taskCompletionSource.SetResult(true);
                };

                TargetView.ExecuteMethod("checkStyleSheetLoaded", "2");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Stylesheet was not loaded!");
                Assert.IsTrue(stylesheet.Contains(".bar"));
            });
        }
    }
}
