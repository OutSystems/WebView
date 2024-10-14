using System;
using System.Collections.Generic;
using System.IO;
using Xilium.CefGlue.Common;

namespace WebViewControl {

    public class GlobalSettings {

        private bool persistCache;
        private bool enableErrorLogOnly;
        private bool osrEnabled = false;
        private string userAgent;
        private string logFile;
        private string cachePath = Path.Combine(Path.GetTempPath(), "WebView" + Guid.NewGuid().ToString().Replace("-", null) + DateTime.UtcNow.Ticks);
        private readonly List<KeyValuePair<string, string>> commandLineSwitches = new();

        /// <summary>
        /// Use this method to pass flags to the browser. List of available flags: https://peter.sh/experiments/chromium-command-line-switches/
        /// </summary>
        public void AddCommandLineSwitch(string key, string value) {
            EnsureNotLoaded(nameof(AddCommandLineSwitch));
            commandLineSwitches.Add(new KeyValuePair<string, string>(key, value));
        }
        
        public IEnumerable<KeyValuePair<string, string>> CommandLineSwitches => commandLineSwitches;

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

        /// <summary>
        /// Set to true to enable off-screen rendering support.
        /// Do not enable this setting if the application does not use off-screen rendering
        /// as it may reduce rendering performance and cause some issues.
        /// </summary>
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
            var port = Environment.GetEnvironmentVariable("WEBVIEW_REMOTE_DEBUGGING_PORT");
            int.TryParse(port != null ? port : "", out var result);
            return result;
#else
            return 0;
#endif
        }
    }
}
