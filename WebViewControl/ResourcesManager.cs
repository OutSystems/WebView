using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    public static class ResourcesManager {

        public static Stream GetResource(string assemblyName, IEnumerable<string> resourcePath) {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) {
                throw new InvalidOperationException("Assembly not found: " + assemblyName);
            }
            return GetResource(assembly, resourcePath);
        }

        public static Stream GetResource(Assembly assembly, IEnumerable<string> resourcePath) {
            var resourceName = assembly.GetName().Name + "." +  string.Join(".", resourcePath);
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) {
                throw new InvalidOperationException("Resource not found: " + resourceName);
            }
            return stream;
        }
    }
}
