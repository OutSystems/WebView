using System;
using System.Collections.Generic;
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
        private List<KeyValuePair<string, string>> flags;

        public void AddCommandLineSwitch(string key, string value) {
            EnsureNotLoaded(nameof(flags));
            if (flags == null) {
                flags = new();
            }
            flags.Add(new KeyValuePair<string, string>(key, value));
        }
        public KeyValuePair<string, string>[] GetCommandLineSwitches() {
            return flags.ToArray();
        }

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
            var port = Environment.GetEnvironmentVariable("WEBVIEW_REMOTE_DEBUGGING_PORT");
            int.TryParse(port != null ? port : "", out var result);
            return result;
#else
            return 0;
#endif
        }
    }
}
