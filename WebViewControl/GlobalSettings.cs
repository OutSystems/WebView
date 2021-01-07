#if REMOTE_DEBUG_SUPPORT
using Microsoft.Extensions.Configuration;
#endif
using System;
using System.IO;
using Xilium.CefGlue.Common;

namespace WebViewControl {

    public class GlobalSettings {

        private bool persistCache;
        private bool enableErrorLogOnly;
        private bool osrEnabled = true;
        private string userAgent;
        private string logFile;
        private string cachePath = Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);

        public string CachePath {
            get => cachePath;
            set {
                EnsureNotLoaded(nameof(CachePath));
                cachePath = value;
            }
        }

        public bool PersistCache {
            get => persistCache;
            set {
                EnsureNotLoaded(nameof(PersistCache));
                persistCache = value;
            }
        }

        public bool EnableErrorLogOnly {
            get => enableErrorLogOnly;
            set {
                EnsureNotLoaded(nameof(EnableErrorLogOnly));
                enableErrorLogOnly = value;
            }
        }

        public string UserAgent {
            get => userAgent;
            set {
                EnsureNotLoaded(nameof(UserAgent));
                userAgent = value;
            }
        }

        public string LogFile {
            get => logFile;
            set {
                EnsureNotLoaded(nameof(LogFile));
                logFile = value;
            }
        }

        public bool OsrEnabled {
            get => osrEnabled;
            set {
                EnsureNotLoaded(nameof(OsrEnabled));
                osrEnabled = value;
            }
        }

        private void EnsureNotLoaded(string propertyName) {
            if (CefRuntimeLoader.IsLoaded) {
                throw new InvalidOperationException($"Cannot set {propertyName} after WebView engine has been loaded");
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
