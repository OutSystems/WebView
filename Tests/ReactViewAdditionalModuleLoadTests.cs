using NUnit.Framework;

namespace Tests {

    public class ReactViewAdditionalModuleLoadTests : ReactViewTestBase {

        protected override void InitializeView() {
            TargetView.AdditionalModule = "ReactViewResources/dist/AdditionalModule.js";
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
