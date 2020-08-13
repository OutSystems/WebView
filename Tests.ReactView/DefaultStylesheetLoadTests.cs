using System.Threading.Tasks;
using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests.ReactView {

    public class DefaultStyleSheetLoadTests : ReactViewTestBase {

        private static ReactViewWithStyleSheet currentView;
        private static bool loadPrimaryStyleSheet = true;

        protected class ViewFactoryWithStyleSheet : TestReactViewFactory {
            public override ResourceUrl DefaultStyleSheet => loadPrimaryStyleSheet ?
                new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "Generated", "Default.css") :
                new ResourceUrl(typeof(DefaultStyleSheetLoadTests).Assembly, "Generated", "Default_2.css");
        }

        protected class ReactViewWithStyleSheet : TestReactView {
            protected override ReactViewFactory Factory => new ViewFactoryWithStyleSheet();

            public void RefreshStyleSheetTests() {
                RefreshDefaultStyleSheet();
            }
        }

        protected override TestReactView CreateView() {
            currentView = new ReactViewWithStyleSheet();
            return currentView;
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
            var initialLoadPrimaryStyleSheet = loadPrimaryStyleSheet;
            loadPrimaryStyleSheet = false;
            
            // Act
            currentView.RefreshStyleSheetTests();

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

            // Tear down
            loadPrimaryStyleSheet = initialLoadPrimaryStyleSheet;
        }
    }
}
