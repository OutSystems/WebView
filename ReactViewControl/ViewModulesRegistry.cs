using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReactViewControl {

    internal class ViewModulesRegistry {

        private static IDictionary<string, Func<IViewModule>> ModuleFactories = new Dictionary<string, Func<IViewModule>>();

        public static void ScanModules(IEnumerable<Assembly> assemblies) {
            foreach(var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    var implementsModuleInterface = type.GetInterface(typeof(IViewModule).FullName) != null;
                    if (implementsModuleInterface && !type.IsAbstract && !type.IsGenericTypeDefinition) {
                        var constructor = type.GetConstructor(new Type[0]);
                        ModuleFactories.Add(type.Name, () => (IViewModule) constructor.Invoke(null));
                    }
                }
            }
        }

        public static IViewModule CreateModuleInstance(string name) {
            if (ModuleFactories.TryGetValue(name + "Module", out var factory)) {
                return factory();
            }
            return null;
        }
    }
}
