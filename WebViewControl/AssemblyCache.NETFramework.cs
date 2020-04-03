using System.Reflection;

namespace WebViewControl {

    partial class AssemblyCache {

        public static Assembly LoadAssembly(string path) => Assembly.LoadFile(path);

    }
}