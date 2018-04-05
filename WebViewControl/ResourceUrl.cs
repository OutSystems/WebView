using System;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    public class ResourceUrl {

        public const string LocalScheme = "local";
        internal const string EmbeddedScheme = "embedded";
        internal const string CustomScheme = "custom";

        private const string AssemblyPathSeparator = ";";
        internal const string PathSeparator = "/";

        private static readonly string DefaultPath = Uri.SchemeDelimiter + "webview/";

        internal static readonly string AssemblyPrefix = EmbeddedScheme + DefaultPath + "assembly:";

        private readonly string url;

        public ResourceUrl(params string[] path) {
            url = string.Join("/", path);
        }

        public ResourceUrl(Assembly assembly, params string[] path) : this(path) {
            var assemblyName = assembly.GetName().Name;
            url = url.StartsWith(PathSeparator) ? url.Substring(1) : (assemblyName + PathSeparator + url);
            url = AssemblyPrefix + assemblyName + AssemblyPathSeparator + url;
        }

        internal ResourceUrl(string scheme, string path) {
            url = scheme + DefaultPath + path;
        }

        public override string ToString() {
            return url;
        }

        /// <summary>
        /// Supported syntax:
        /// embedded://webview/assembly:AssemblyName;Path/To/Resource
        /// embedded://webview/AssemblyName/Path/To/Resource (AssemblyName is also assumed as default namespace)
        /// </summary>
        internal static string[] GetEmbeddedResourcePath(Uri resourceUrl) {
            if (resourceUrl.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                var indexOfPath = resourceUrl.AbsolutePath.IndexOf(AssemblyPathSeparator);
                return resourceUrl.AbsolutePath.Substring(indexOfPath + 1).Split(new [] { PathSeparator }, StringSplitOptions.None);
            }
            var uriParts = resourceUrl.Segments;
            return uriParts.Skip(1).Select(p => p.Replace(PathSeparator, "")).ToArray();
        }

        /// <summary>
        /// Supported syntax:
        /// embedded://webview/assembly:AssemblyName;Path/To/Resource
        /// embedded://webview/AssemblyName/Path/To/Resource (AssemblyName is also assumed as default namespace)
        /// </summary>
        internal static string GetEmbeddedResourceAssemblyName(Uri url) {
            if (url.AbsoluteUri.StartsWith(AssemblyPrefix)) {
                var resourcePath = url.AbsoluteUri.Substring(AssemblyPrefix.Length);
                var indexOfPath = Math.Max(0, resourcePath.IndexOf(AssemblyPathSeparator));
                return resourcePath.Substring(0, indexOfPath);
            }
            if (url.Segments.Length > 1) {
                var assemblySegment = url.Segments[1];
                return assemblySegment.EndsWith(PathSeparator) ? assemblySegment.Substring(0, assemblySegment.Length - PathSeparator.Length) : assemblySegment; // default assembly name to the first path
            }
            return string.Empty;
        }
    }
}
