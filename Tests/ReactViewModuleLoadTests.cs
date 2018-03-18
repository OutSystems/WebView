using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class ReactViewModuleLoadTests : ReactViewTestBase {

        class AdditionalModule : ViewModuleContainer {

            internal interface IProperties {
            }

            private class Properties : IProperties {
                protected readonly AdditionalModule owner;
                public Properties(AdditionalModule owner) {
                    this.owner = owner;
                }
            }

            protected override string JavascriptSource => "/Tests/ReactViewResources/dist/AdditionalModule.js";
            protected override string JavascriptName => "Common";

            protected override object CreateNativeObject() {
                return new Properties(this);
            }
        }

        protected override void InitializeView() {
            TargetView.Modules = new[] { new AdditionalModule() };
            base.InitializeView();
        }

        [Test(Description = "Tests additional module is loaded")]
        public void AdditionalModuleIsLoaded() {
            var additionalModuleLoaded = false;
            TargetView.Event += (args) => {
                additionalModuleLoaded = args == "AdditionalModuleLoaded";
            };

            TargetView.ExecuteMethodOnRoot("checkAdditionalModuleLoaded");

            WaitFor(() => additionalModuleLoaded, "additional module load");
        }
    }
}
