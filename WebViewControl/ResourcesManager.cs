using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    public static class ResourcesManager {

        private static Stream InternalTryGetResource(string assemblyName, string defaultNamespace, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) {
                if (failOnMissingResource) {
                    throw new InvalidOperationException("Assembly not found: " + assemblyName);
                }
                return null;
            }
            return InternalTryGetResource(assembly, defaultNamespace, resourcePath, failOnMissingResource);
        }

        private static Stream InternalTryGetResource(Assembly assembly, string defaultNamespace, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var resourceName = string.Join(".", (new[] { defaultNamespace }).Concat(resourcePath));
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (failOnMissingResource && stream == null) {
                throw new InvalidOperationException("Resource not found: " + resourceName);
            }
            return stream;
        }

        public static Stream GetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, resourcePath.First(), resourcePath.Skip(1), true);
        }

        public static Stream GetResource(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, assembly.GetName().Name, resourcePath, true);
        }

        public static Stream GetResource(string assemblyName, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assemblyName, assemblyName, resourcePath, true);
        }

        public static Stream TryGetResource(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, assembly.GetName().Name, resourcePath, false);
        }

        public static Stream TryGetResource(string assemblyName, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assemblyName, assemblyName, resourcePath, false);
        }

        public static Stream TryGetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, resourcePath.First(), resourcePath.Skip(1), false);
        }

        public static Stream TryGetResourceWithFullPath(string assemblyName, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assemblyName, resourcePath.First(), resourcePath.Skip(1), false);
        }

        public static string GetMimeType(string resourceName) {
            var extension = Path.GetExtension(resourceName);
            return CefSharp.ResourceHandler.GetMimeType(extension);
        }
    }
}
