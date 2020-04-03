using System.Reflection;

namespace WebViewControl {

    internal static class AssemblyLoader {

        internal static Assembly LoadAssembly(string path) => Assembly.LoadFile(path);

    }
}