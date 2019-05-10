﻿using NUnit.Framework;
using ReactViewControl;
using WebViewControl;

namespace Tests {

    public class ReactViewPluginModulesLoadTests : ReactViewTestBase {

        private class ViewFactoryWithPlugin : TestReactViewFactory {
            public override IViewModule[] Plugins => new[] { new PluginModule() };
        }

        private class ReactViewWithPlugin : TestReactView {

            public ReactViewWithPlugin() {
                AddMappings(new SimpleViewModule("SimpleModuleWithAlias", new ResourceUrl(typeof(ReactViewWithPlugin).Assembly, "ReactViewResources", "Test", "AliasedModule.js")));
            }

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

        [Test(Description = "Tests module with alias is loaded")]
        public void AliasedModuleIsLoaded() {
            var pluginModuleLoaded = false;
            TargetView.Event += (args) => {
                pluginModuleLoaded = args == "AliasedModuleLoaded";
            };

            TargetView.ExecuteMethodOnRoot("checkAliasedModuleLoaded");

            WaitFor(() => pluginModuleLoaded, "aliased module load");
        }
    }
}
