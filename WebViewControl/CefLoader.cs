using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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

        /// <summary>
        /// Helper method to get the Xilium.CefGlue.BrowserProcess.exe file path
        /// </summary>
        public static string GetBrowserSubProcessPath() {
            const string BrowserProcessName = "Xilium.CefGlue.BrowserProcess.exe";

            var path = GetProbingPaths().Select(p => Path.Combine(p, BrowserProcessName)).FirstOrDefault(File.Exists);

            if (path == null) {
                throw new FileNotFoundException("Unable to locate", BrowserProcessName);
            }

            return path;
        }

        private static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Xilium.CefGlue")) {
                var assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                if (!assemblyName.EndsWith(".resources.dll")) {
                    var archSpecificPath = GetProbingPaths().Select(p => Path.Combine(p, assemblyName)).FirstOrDefault(File.Exists);

                    if (archSpecificPath == null) {
                        throw new FileNotFoundException("Unable to locate", assemblyName);
                    }

                    return Assembly.LoadFile(archSpecificPath);
                }
            }
            return null;
        }

        private static IEnumerable<string> GetProbingPaths() {
            var basePath = Path.GetDirectoryName(typeof(CefLoader).Assembly.Location); ;
            yield return Path.Combine(basePath, "x64");
        }
    }
}
