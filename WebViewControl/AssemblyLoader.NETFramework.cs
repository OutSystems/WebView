using System.Reflection;

namespace WebViewControl {

    internal class AssemblyLoader {

        internal static Assembly LoadAssembly(string path) => Assembly.LoadFile(path);

    }
}