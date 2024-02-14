using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WebViewControl {

     internal class AssemblyCache {

        private object SyncRoot { get; } = new object();

        // We now allow to load multiple versions of the same assembly, which means that resource urls can
        // (optionally) specify the version. We don't force the version to be specified to maintain backwards
        // compatibility, and thus need to store assemblies by their name, in addition to by (name,version).
        // Note that assembliesByName naturally doesn't handle duplicates, and no guarantee is provided about
        // which version is resolved if there are multiple loaded assemblies with the same name.
        // This, consumer apps are encouraged to include the version in the resource url
        private Dictionary<(string, Version), Assembly> assembliesByNameAndVersion;
        private Dictionary<string, Assembly> assembliesByName;
        
        private bool newAssembliesLoaded = true;

        internal Assembly ResolveResourceAssembly(Uri resourceUrl, bool failOnMissingAssembly) {
            if (assembliesByNameAndVersion == null) {
                lock (SyncRoot) {
                    if (assembliesByNameAndVersion == null) {
                        assembliesByNameAndVersion = new Dictionary<(string, Version), Assembly>();
                        assembliesByName = new Dictionary<string, Assembly>();
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
                                var domainAssemblyName = domainAssembly.GetName().Name;
                                var domainAssemblyVersion = domainAssembly.GetName().Version;
                                // replace if duplicated (can happen)
                                assembliesByNameAndVersion[(domainAssemblyName, domainAssemblyVersion)] = domainAssembly;
                                assembliesByName[domainAssemblyName] = domainAssembly;
                            }
                        }
                    }
                }

                assembly = GetAssemblyByNameAndVersion(assemblyName, assemblyVersion);
                if (assembly == null) {
                    try {
                        // try load assembly from its name
                        var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
                        assembly = AssemblyLoader.LoadAssembly(assemblyPath);
                    } catch (IOException) { 
                        // ignore
                    }

                    if (assembly != null) {
                        lock (SyncRoot) {
                            assembliesByNameAndVersion[(assembly.GetName().Name, assembly.GetName().Version)] = assembly;
                            assembliesByName[assembly.GetName().Name] = assembly;
                        }
                    }
                }
            }

            if (failOnMissingAssembly && assembly == null) {
                throw new InvalidOperationException("Could not find assembly for: " + resourceUrl);
            }
            return assembly;
        }

        private Assembly GetAssemblyByNameAndVersion(string assemblyName, Version assemblyVersion) {
            lock (SyncRoot) {
                Assembly assembly;
                if (assemblyVersion == null) {
                    assembliesByName.TryGetValue(assemblyName, out assembly);
                } else {
                    assembliesByNameAndVersion.TryGetValue((assemblyName, assemblyVersion), out assembly);
                }
                return assembly;
            }
        }
    }
}