using NUnit.Framework;
using ReactViewControl;

namespace Tests {

    public class ReactViewPluginModulesLoadTests : ReactViewTestBase {

        protected class ViewFactoryWithPlugin : TestReactViewFactory {
            public override IViewModule[] Plugins => new[] { new PluginModule() };
        }

        protected class ReactViewWithPlugin : TestReactView {
            protected override ReactViewFactory Factory => new ViewFactoryWithPlugin();
        }

        protected override TestReactView CreateView() {
            return new ReactViewWithPlugin();
        }

        class PluginModule : ViewModuleContainer {

            internal interface IProperties {
            }

            private class Properties : IProperties {
                protected readonly PluginModule owner;
                public Properties(PluginModule owner) {
                    this.owner = owner;
                }
            }

            protected override string JavascriptSource => "/Tests/ReactViewResources/Test/PluginModule.js";
            protected override string NativeObjectName => "Common";
            protected override string ModuleName => "Plugin/With/Slashes/On/Name";
            protected override object CreateNativeObject() {
                return new Properties(this);
            }
        }

        [Test(Description = "Tests plugin module is loaded")]
        public void PluginModuleIsLoaded() {
            var pluginModuleLoaded = false;
            TargetView.Event += (args) => {
                pluginModuleLoaded = args == "PluginModuleLoaded";
            };

            TargetView.ExecuteMethodOnRoot("checkPluginModuleLoaded");

            WaitFor(() => pluginModuleLoaded, "plugin module load");
        }
    }
}
