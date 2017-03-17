using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    public static class ResourcesManager {

        private static Stream InternalTryGetResource(string assemblyName, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) {
                if (failOnMissingResource) {
                    throw new InvalidOperationException("Assembly not found: " + assemblyName);
                }
                return null;
            }
            return InternalTryGetResource(assembly, resourcePath, failOnMissingResource);
        }

        private static Stream InternalTryGetResource(Assembly assembly, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var resourceName = assembly.GetName().Name + "." + string.Join(".", resourcePath);
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (failOnMissingResource && stream == null) {
                throw new InvalidOperationException("Resource not found: " + resourceName);
            }
            return stream;
        }

        public static Stream GetResource(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, resourcePath, true);
        }

        public static Stream GetResource(string assemblyName, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assemblyName, resourcePath, true);
        }

        public static Stream TryGetResource(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, resourcePath, false);
        }

        public static Stream TryGetResource(string assemblyName, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assemblyName, resourcePath, false);
        }

        public static string GetMimeType(string resourceName) {
            var extension = Path.GetExtension(resourceName);
            return CefSharp.ResourceHandler.GetMimeType(extension);
        }
    }
}
