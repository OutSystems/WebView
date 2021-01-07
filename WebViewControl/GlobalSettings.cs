#if REMOTE_DEBUG_SUPPORT
using Microsoft.Extensions.Configuration;
#endif
using System;
using System.IO;
using Xilium.CefGlue.Common;

namespace WebViewControl {

    public class GlobalSettings {

        private bool osrEnabled = true;

        public string UserAgent { get; set; }

        public string LogFile { get; set; }

        public string CachePath { get; set; } = Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);

        public bool PersistCache { get; set; } = false;

        public bool EnableErrorLogOnly { get; set; } = false;

        public bool OsrEnabled {
            get => osrEnabled;
            set {
                if (CefRuntimeLoader.IsLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(OsrEnabled)} after WebView engine has been loaded");
                }
                osrEnabled = value;
            }
        }

        internal int GetRemoteDebuggingPort() {
#if REMOTE_DEBUG_SUPPORT
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            var configuration = configurationBuilder.Build();
            var port = configuration["RemoteDebuggingPort"];
            int.TryParse(port != null ? port : "", out var result);
            return result;
#else
            return 0;
#endif
        }
    }
}
