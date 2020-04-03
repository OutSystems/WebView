using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using WebViewControl;

namespace Xilium.CefGlue {

    /// <summary>
    /// Used for AnyCPU Support to resolve Assemblies and Dependencies
    /// </summary>
    internal static class CefLoader {

        /// <summary>
        /// RegisterCefGlueAssemblyResolver
        /// </summary>
        public static void RegisterCefGlueAssemblyResolver() {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        /// <summary>
        /// UnRegisterCefGlueAssemblyResolver
        /// </summary>
        public static void UnRegisterCefGlueAssemblyResolver() {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolver;
        }

        private static Assembly Resolver(object sender, ResolveEventArgs args) {
            if (args.Name.StartsWith("Xilium.CefGlue")) {
                var assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                if (!assemblyName.EndsWith(".resources.dll")) {
                    var archSpecificPath = GetProbingPaths().Select(p => Path.Combine(p, assemblyName)).FirstOrDefault(File.Exists);

                    if (archSpecificPath == null) {
                        throw new FileNotFoundException("Unable to locate", assemblyName);
                    }

                    return AssemblyCache.LoadAssembly(archSpecificPath);
                }
            }
            return null;
        }

        private static IEnumerable<string> GetProbingPaths() {
            var basePath = Path.GetDirectoryName(typeof(CefLoader).Assembly.Location);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                yield return Path.Combine(basePath, "x64");
            } else {
                yield return basePath;
            }
        }
    }
}
