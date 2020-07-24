using System;

namespace WebViewControl {

    internal class BrowserObjectListener {

        public event Action<string, object[]> NotificationReceived;

        public void Notify(string listenerName, params object[] args) {
            NotificationReceived?.Invoke(listenerName, args ?? new object[0]);
        }
    }
}
