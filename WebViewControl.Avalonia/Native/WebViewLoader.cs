using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace WebViewControl {

    /// <summary>
    /// The native engines are part of the operating system, there's no runtime to start: this only applies the global settings.
    /// </summary>
    internal static class WebViewLoader {

        private static GlobalSettings globalSettings;

        public static bool IsLoaded { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Initialize(GlobalSettings settings) {
            if (IsLoaded) {
                return;
            }

            globalSettings = settings;

            if (settings.EnableVideoAutoplay) {
                // used by WebView2 (WKWebView reads the setting when creating each web view)
                settings.AddCommandLineSwitch("autoplay-policy", "no-user-gesture-required");
            }

            IsLoaded = true;

            AppDomain.CurrentDomain.ProcessExit += delegate { Cleanup(); };
        }

        /// <summary>
        /// Release all resources and shutdown web view
        /// </summary>
        [DebuggerNonUserCode]
        public static void Cleanup() {
            if (globalSettings == null || globalSettings.PersistCache) {
                return;
            }

            try {
                var dirInfo = new DirectoryInfo(globalSettings.CachePath);
                if (dirInfo.Exists) {
                    dirInfo.Delete(true);
                }
            } catch (UnauthorizedAccessException) {
                // ignore, the engine processes may still be using the files
            } catch (IOException) {
                // ignore, the engine processes may still be using the files
            }
        }
    }
}
