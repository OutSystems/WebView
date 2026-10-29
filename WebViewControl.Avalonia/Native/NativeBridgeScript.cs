using System;
using System.IO;
using System.Reflection;

namespace WebViewControl.Native {

    /// <summary>
    /// The javascript injected in every document, see Resources/NativeBridge.js.
    /// </summary>
    internal static class NativeBridgeScript {

        private static readonly Lazy<string> source = new Lazy<string>(() => {
            using (var stream = typeof(NativeBridgeScript).Assembly.GetManifestResourceStream("WebViewControl.Native.NativeBridge.js")) {
                if (stream == null) {
                    throw new InvalidOperationException("Native bridge script resource not found");
                }
                using (var reader = new StreamReader(stream)) {
                    return reader.ReadToEnd();
                }
            }
        });

        public static string Source => source.Value;
    }
}
