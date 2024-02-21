using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

     internal class AssemblyCache {

        private object SyncRoot { get; } = new object();

        // We now allow to load multiple versions of the same assembly, which means that resource urls can
        // (optionally) specify the version. We don't force the version to be specified to maintain backwards
        // compatibility, and thus for each assembly name we two entries in the dictionary: with and without a version.
        // Note that no guarantee is provided about which version is resolved if there are multiple loaded assemblies
        // with the same name and no specific version is provided.
        // This, consumer apps are encouraged to include the version in the resource url
        private IDictionary<(string AssemblyName, Version AssemblyVersion), Assembly> assemblies;
        
        private bool newAssembliesLoaded = true;

        internal Assembly ResolveResourceAssembly(Uri resourceUrl, bool failOnMissingAssembly) {
            if (assemblies == null) {
                lock (SyncRoot) {
                    if (assemblies == null) {
                        assemblies = new Dictionary<(string, Version), Assembly>();
                        AppDomain.CurrentDomain.AssemblyLoad += delegate { newAssembliesLoaded = true; };
                    }
                }
            }

            var (assemblyName, assemblyVersion) = ResourceUrl.GetEmbeddedResourceAssemblyNameAndVersion(resourceUrl);
            var assembly = GetAssemblyByNameAndVersion(assemblyName, assemblyVersion);

            if (assembly == null) {
                if (newAssembliesLoaded) {
                    lock (SyncRoot) {
                        if (newAssembliesLoaded) {
                            // add loaded assemblies to cache
                            newAssembliesLoaded = false;
                            foreach (var domainAssembly in AppDomain.CurrentDomain.GetAssemblies()) {
                                AddOrReplace(domainAssembly);
                            }
                        }
                    }
                }

                assembly = GetAssemblyByNameAndVersion(assemblyName, assemblyVersion);
                if (assembly == null) {
                    try {
                        // try loading the assembly from a file named AssemblyName.dll (or AssemblyName-AssemblyVersion.dll if
                        // a version was provided)
                        var fileName = $"{assemblyName}{(assemblyVersion == null ? "" : $"-{assemblyVersion}")}.dll";
                        var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                        assembly = AssemblyLoader.LoadAssembly(assemblyPath);
                    } catch (IOException) { 
                        // ignore
                    }

                    if (assembly != null) {
                        lock (SyncRoot) {
                            AddOrReplace(assembly);
                        }
                    }
                }
            }

            if (failOnMissingAssembly && assembly == null) {
                throw new InvalidOperationException("Could not find assembly for: " + resourceUrl);
            }
            return assembly;
        }

        private void AddOrReplace(Assembly assembly) {
            var identity = assembly.GetName();
            var assemblyName = identity.Name;
            if (assemblyName == null) {
                return;
            }

            // add two entries, with and without the version.
            // for the null-version entry, keep the assembly with the highest version
            var version = identity.Version;
            if (!assemblies.TryGetValue((assemblyName, null), out var nullVersionAssembly) ||
                (nullVersionAssembly.GetName().Version is { } previousVersion && previousVersion < version)) {
                assemblies[(assemblyName, null)] = assembly;
            }
            assemblies[(assemblyName, version)] = assembly;
        }

        private Assembly GetAssemblyByNameAndVersion(string assemblyName, Version assemblyVersion) {
            lock (SyncRoot) {
                assemblies.TryGetValue((assemblyName, assemblyVersion), out var assembly);
                return assembly;
            }
        }
    }
}