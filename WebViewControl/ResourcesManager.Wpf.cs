using System;
using System.IO;
using System.Windows;

namespace WebViewControl {

    partial class ResourcesManager {

        private static Stream GetApplicationResource(string assemblyName, string resourceName) {
            return Application.GetResourceStream(new Uri($"/{assemblyName};component/{resourceName}", UriKind.Relative))?.Stream;
        }
    }
}
