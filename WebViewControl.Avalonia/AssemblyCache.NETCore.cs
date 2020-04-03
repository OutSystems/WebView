using System.Reflection;
using System.Runtime.Loader;

namespace WebViewControl {

    partial class AssemblyCache {

        public static Assembly LoadAssembly(string path) => AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

    }
}