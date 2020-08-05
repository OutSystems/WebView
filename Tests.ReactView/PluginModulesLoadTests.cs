using System.Threading.Tasks;
using NUnit.Framework;
using ReactViewControl;

namespace Tests.ReactView {

    public class PluginModulesLoadTests : ReactViewTestBase {

        private class ViewFactoryWithPlugin : TestReactViewFactory {

            public override IViewModule[] InitializePlugins() {
                return new IViewModule[] { new PluginModule(), new AliasedModule() };
            }
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

            protected override string MainJsSource => "/Tests.ReactView/Generated/PluginModule.js";
            protected override string NativeObjectName => nameof(PluginModule);
            protected override string ModuleName => "PluginModule";
            protected override object CreateNativeObject() {
                return new Properties(this);
            }
        }

        class AliasedModule : ViewModuleContainer {

            internal interface IProperties {
            }

            private class Properties : IProperties {
                protected AliasedModule Owner { get; }
                public Properties(AliasedModule owner) {
                    Owner = owner;
                }
            }

            protected override string MainJsSource => "/Tests.ReactView/Generated/AliasedModule.js";
            protected override string NativeObjectName => nameof(AliasedModule);
            protected override string ModuleName => "AliasedModule";
            protected override object CreateNativeObject() {
                return new Properties(this);
            }
        }

        [Test(Description = "Tests plugin module is loaded")]
        public async Task PluginModuleIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<string>();

                TargetView.Event += (args) => taskCompletionSource.SetResult(args);
                TargetView.ExecuteMethod("checkPluginModuleLoaded");
                var eventArg = await taskCompletionSource.Task;

                Assert.AreEqual("PluginModuleLoaded", eventArg, "Plugin module was not loaded!");
            });
        }

        [Test(Description = "Tests module with alias is loaded")]
        public async Task AliasedModuleIsLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<string>();

                TargetView.Event += (args) => taskCompletionSource.SetResult(args);
                TargetView.ExecuteMethod("checkAliasedModuleLoaded");
                var eventArg = await taskCompletionSource.Task;

                Assert.AreEqual("AliasedModuleLoaded", eventArg, "Aliased module was not loaded!");
            });
        }
    }
}
