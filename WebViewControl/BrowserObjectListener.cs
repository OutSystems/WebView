using System;
using System.Windows;

namespace WebViewControl {

    internal class BrowserObjectListener {

        public event Action<string> NotificationReceived;

        public void Notify(string listenerName) {
            // BeginInvoke otherwise if we try to execute some script  on the browser as a result of this notification, it will block forever
            Application.Current.Dispatcher.BeginInvoke(
                (Action)(() => {
                    NotificationReceived?.Invoke(listenerName);
                }));
        }
    }
}
