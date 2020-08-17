using System.Threading.Tasks;
using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests.ReactView {

    public class DefaultStyleSheetLoadTests : ReactViewTestBase {

        protected class ViewFactoryWithStyleSheet : TestReactViewFactory {
            private ResourceUrl defaultStyleSheet;

            public ViewFactoryWithStyleSheet(bool loadPrimaryStyleSheet) : base() {
                defaultStyleSheet = loadPrimaryStyleSheet ?
                    new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "Generated", "Default.css") :
                    new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "Generated", "Default_2.css");
            }

            public override ResourceUrl DefaultStyleSheet => defaultStyleSheet;
        }

        protected class ReactViewWithStyleSheet : TestReactView {
            protected override ReactViewFactory Factory => new ViewFactoryWithStyleSheet(LoadPrimaryStyleSheet);

            public void RefreshStyleSheetTests() {
                RefreshDefaultStyleSheet();
            }

            public bool LoadPrimaryStyleSheet { get; set; } = true;
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

                StringAssert.Contains(".bar", stylesheet);
            });
        }

        [Test(Description = "Tests default stylesheets get loaded when refreshed")]
        public async Task SecondaryStylesheetIsLoadedWhenDefaultStyleSheetIsRefreshed() {
            // Arrange
            var view = TargetView as ReactViewWithStyleSheet;
            view.LoadPrimaryStyleSheet = false;

            // Act
            view.RefreshStyleSheetTests();

            // Asssert
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<string>();

                TargetView.Event += (args) => {
                    taskCompletionSource.SetResult(args);
                };

                TargetView.ExecuteMethod("checkStyleSheetLoaded", "2");
                var stylesheet = await taskCompletionSource.Task;

                StringAssert.Contains(".bazz-version-2", stylesheet);
            });
        }
    }
}
