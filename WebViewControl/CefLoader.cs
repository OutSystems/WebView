using System;
using System.IO;
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
            var path = Path.Combine("x64", BrowserProcessName);
            if (!File.Exists(path)) {
                throw new FileNotFoundException("Unable to locate", path);
            }
            return path;
        }

        private static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Xilium.CefGlue")) {
                var assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                var archSpecificPath = Path.Combine(GetBasePath(), "x64", assemblyName);

                if (!File.Exists(archSpecificPath)) {
                    if (!archSpecificPath.EndsWith(".resources.dll")) {
                        throw new FileNotFoundException("Unable to locate", archSpecificPath);
                    }
                } else {
                    return Assembly.LoadFile(archSpecificPath);
                }
            }
            return null;
        }

        private static string GetBasePath() {
            return Path.GetDirectoryName(typeof(CefLoader).Assembly.Location);
        }
    }
}
