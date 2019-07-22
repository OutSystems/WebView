using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    internal class ResourcesProvider : IDisposable {

        private Dictionary<string, Assembly> assemblies;
        private bool newAssembliesLoaded = true;

        public void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
            var resourceAssembly = ResolveResourceAssembly(url);
            var resourcePath = ResourceUrl.GetEmbeddedResourcePath(url);

            var extension = Path.GetExtension(resourcePath.Last()).ToLower();

            var resourceStream = TryGetResourceWithFullPath(resourceAssembly, resourcePath);
            if (resourceStream != null) {
                resourceHandler.RespondWith(resourceStream, extension);
            }
        }

        protected Assembly ResolveResourceAssembly(Uri resourceUrl) {
            if (assemblies == null) {
                assemblies = new Dictionary<string, Assembly>();
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            }

            var assemblyName = ResourceUrl.GetEmbeddedResourceAssemblyName(resourceUrl);
            var assembly = GetAssemblyByName(assemblyName);

            if (assembly == null) {
                if (newAssembliesLoaded) {
                    // add loaded assemblies to cache
                    newAssembliesLoaded = false;
                    foreach (var domainAssembly in AppDomain.CurrentDomain.GetAssemblies()) {
                        // replace if duplicated (can happen)
                        assemblies[domainAssembly.GetName().Name] = domainAssembly;
                    }
                }

                assembly = GetAssemblyByName(assemblyName);
                if (assembly == null) {
                    // try load assembly from its name
                    assembly = AppDomain.CurrentDomain.Load(new AssemblyName(assemblyName));
                    if (assembly != null) {
                        assemblies[assembly.GetName().Name] = assembly;
                    }
                }
            }

            if (assembly != null) {
                return assembly;
            }

            throw new InvalidOperationException("Could not find assembly for: " + resourceUrl);
        }

        private Assembly GetAssemblyByName(string assemblyName) {
            assemblies.TryGetValue(assemblyName, out var assembly);
            return assembly;
        }

        private Stream TryGetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
            return ResourcesManager.TryGetResourceWithFullPath(assembly, resourcePath);
        }

        private void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args) {
            newAssembliesLoaded = true;
        }

        public void Dispose() {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
        }
    }
}
