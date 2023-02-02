using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xilium.CefGlue;

namespace WebViewControl {

    public static partial class ResourcesManager {

        private static readonly AssemblyCache cache = new AssemblyCache();

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
                resourceParts[i] = resourceParts[i].Replace('-', '_').Replace('@', '_');
            }
            return string.Join(".", resourceParts);
        }

        private static Stream InternalTryGetResource(Assembly assembly, string defaultNamespace, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var resourceName = ComputeEmbeddedResourceName(defaultNamespace, resourcePath);
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) {
                var assemblyName = assembly.GetName().Name;
                var alternativeResourceName = string.Join(ResourceUrl.PathSeparator, resourcePath);
                try {
                    stream = GetApplicationResource(assemblyName, alternativeResourceName);
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

        internal static Stream TryGetResource(Uri url, bool failOnMissingAssembly, out string extension) {
            var resourceAssembly = cache.ResolveResourceAssembly(url, failOnMissingAssembly);
            if (resourceAssembly == null) {
                extension = string.Empty;
                return null;
            }
            var resourcePath = ResourceUrl.GetEmbeddedResourcePath(url);

            extension = Path.GetExtension(resourcePath.Last()).ToLower();
            var resourceStream = TryGetResourceWithFullPath(resourceAssembly, resourcePath);

            return resourceStream;
        }

        public static Stream TryGetResource(Uri url) {
            return TryGetResource(url, false, out _);
        }

        public static string GetMimeType(string resourceName) {
            var extension = Path.GetExtension(resourceName);
            return GetExtensionMimeType(extension);
        }

        public static string GetExtensionMimeType(string extension) {
            extension = string.IsNullOrEmpty(extension) ? "html" : extension.TrimStart('.');
            return CefRuntime.GetMimeType(extension);
        }
    }
}
