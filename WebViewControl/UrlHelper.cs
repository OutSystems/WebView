using System;

namespace WebViewControl {

    internal static class UrlHelper {

        private const string ChromeInternalProtocol = "chrome-devtools:";

        public const string AboutBlankUrl = "about:blank";

        public static bool IsChromeInternalUrl(string url) {
            return url != null && url.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
