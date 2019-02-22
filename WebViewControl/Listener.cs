using System;
using System.Linq;

namespace WebViewControl {

    public class Listener : IDisposable {

        internal const string EventListenerObjName = "__WebviewListener__";

        private readonly string eventName;
        private readonly Action<Action, bool> handlerWrapper;
        private readonly BrowserObjectListener underlyingListener;

        internal Listener(string eventName, Action<Action, bool> handlerWrapper, BrowserObjectListener underlyingListener) {
            this.eventName = eventName;
            this.handlerWrapper = handlerWrapper;
            this.underlyingListener = underlyingListener;

            underlyingListener.NotificationReceived += HandleEvent;
        }
        
        private void HandleEvent(string eventName) {
            if (this.eventName == eventName) {
                foreach(Action handler in Handler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    handlerWrapper(handler, false);
                }
                foreach (Action handler in UIHandler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    handlerWrapper(handler, true);
                }
            }
        }

        public override string ToString() {
            return $"({EventListenerObjName}.notify('{eventName}'));";
        }

        public event Action Handler;

        public event Action UIHandler;

        public void Dispose() {
            underlyingListener.NotificationReceived -= HandleEvent;
        }
    }
}
