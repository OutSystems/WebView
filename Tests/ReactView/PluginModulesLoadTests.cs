using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests.ReactView {

    public class PluginModulesLoadTests : ReactViewTestBase {

        private class ViewFactoryWithPlugin : TestReactViewFactory {
            public override IViewModule[] Plugins => new[] { new PluginModule() };
        }

        private class ReactViewWithPlugin : TestReactView {

            public ReactViewWithPlugin() { }

            protected override ReactViewFactory Factory => new ViewFactoryWithPlugin();
        }

        protected override TestReactView CreateView() {
            return new ReactViewWithPlugin();
        }

        class PluginModule : ViewModuleContainer {

            internal interface IProperties {
            }

            private class Properties : IProperties {
                protected PluginModule Owner { get; }
                public Properties(PluginModule owner) {
                    Owner = owner;
                }
            }

            protected override string MainJsSource => "/Tests/ReactViewResources/Test/PluginModule.js";
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

            TargetView.ExecuteMethod("checkPluginModuleLoaded");

            WaitFor(() => pluginModuleLoaded, "plugin module load");
        }

        [Test(Description = "Tests module with alias is loaded")]
        public void AliasedModuleIsLoaded() {
            var pluginModuleLoaded = false;
            TargetView.Event += (args) => {
                pluginModuleLoaded = args == "AliasedModuleLoaded";
            };

            TargetView.ExecuteMethod("checkAliasedModuleLoaded");

            WaitFor(() => pluginModuleLoaded, "aliased module load");
        }
    }
}
