using System;

namespace WebViewControl {

    internal class BrowserObjectListener {

        public event Action<string> NotificationReceived;

        public void Notify(string listenerName) {
            NotificationReceived?.Invoke(listenerName);
        }
    }
}
