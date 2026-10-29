using System;
using System.Runtime.InteropServices;
using WebViewControl.Native.Mac;

namespace WebViewControl.Native {

    internal static class NativeBrowserFactory {

        public static INativeBrowser Create(INativeBrowserOwner owner) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return new MacBrowser(owner);
            }
            throw new PlatformNotSupportedException($"There is no native web view implementation for {RuntimeInformation.OSDescription} yet.");
        }

        /// <summary>
        /// Runs a slice of the application's event loop, see <see cref="WebView.WaitForResult{T}"/>.
        /// </summary>
        public static void ProcessEvents(int milliseconds) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                MacEventLoop.ProcessEvents(milliseconds);
            } else {
                System.Threading.Thread.Sleep(milliseconds);
            }
        }
    }
}
