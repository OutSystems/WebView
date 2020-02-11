using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Styling;

namespace ReactViewControl {

    partial class ReactViewRender : ContentControl, IStyleable {

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            Content = WebView;
        }

        private IntPtr GetHostViewHandle() {
            var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
            var windowHandle = (appLifetime.MainWindow ?? appLifetime.Windows.FirstOrDefault())?.PlatformImpl.Handle;

            if (windowHandle is IMacOSTopLevelPlatformHandle macOSHandle) {
                return macOSHandle.GetNSViewRetained();
            }
            return windowHandle?.Handle ?? IntPtr.Zero;
        }
    }
}
