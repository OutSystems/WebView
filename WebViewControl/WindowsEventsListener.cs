using System;
using System.Windows;

namespace WebViewControl {

    internal static class WindowsEventsListener {

        static WindowsEventsListener() {
            EventManager.RegisterClassHandler(typeof(Window), Window.UnloadedEvent, new RoutedEventHandler(OnWindowUnloaded), true);
        }

        public static event Action<Window> WindowUnloaded;

        private static void OnWindowUnloaded(object sender, EventArgs e) {
            WindowUnloaded?.Invoke((Window) sender);
        }
    }
}
