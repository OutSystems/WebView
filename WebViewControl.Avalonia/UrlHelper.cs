using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WebViewControl {

    internal static class UrlHelper {

        private const string ChromeInternalProtocol = "devtools:";

        public const string AboutBlankUrl = "about:blank";

        public static ResourceUrl DefaultLocalUrl = new ResourceUrl(ResourceUrl.LocalScheme, "index.html");

        public static bool IsChromeInternalUrl(string url) {
            return url != null && url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsInternalUrl(string url) {
            return IsChromeInternalUrl(url) || url.StartsWith(DefaultLocalUrl.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        public static void OpenInExternalBrowser(string url) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process.Start("explorer", "\"" + url + "\"");
            } else {
                Process.Start("open", url);
            }
        }
    }
}
