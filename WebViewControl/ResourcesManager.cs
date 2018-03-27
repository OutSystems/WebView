using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

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

        private static string ComputeEmbeddedResourceName(string defaultNamespace, IEnumerable<string> resourcePath) {
            var resourceParts = (new[] { defaultNamespace }).Concat(resourcePath).ToArray();
            for (int i = 0; i < resourceParts.Length - 1; i++) {
                resourceParts[i] = resourceParts[i].Replace('-', '_');
            }
            return string.Join(".", resourceParts);
        }

        private static Stream InternalTryGetResource(Assembly assembly, string defaultNamespace, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var resourceName = ComputeEmbeddedResourceName(defaultNamespace, resourcePath);
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) {
                var assemblyName = assembly.GetName().Name;
                var alternativeResourceName = string.Join("/", resourcePath);
                try {
                    stream = Application.GetResourceStream(new Uri($"/{assemblyName};component/{alternativeResourceName}", UriKind.Relative))?.Stream;
                } catch (IOException) {
                    // ignore
                }
            }
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
