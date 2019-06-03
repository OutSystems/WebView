using System;
using System.Linq;

namespace WebViewControl {

    public class Listener : IDisposable {

        internal const string EventListenerObjName = "__WebviewListener__";

        private string EventName { get; }
        private Action<Action, bool> HandlerWrapper { get; }
        private BrowserObjectListener UnderlyingListener { get; }

        internal Listener(string eventName, Action<Action, bool> handlerWrapper, BrowserObjectListener underlyingListener) {
            EventName = eventName;
            HandlerWrapper = handlerWrapper;
            UnderlyingListener = underlyingListener;

            UnderlyingListener.NotificationReceived += HandleEvent;
        }
        
        private void HandleEvent(string eventName) {
            if (this.EventName == eventName) {
                foreach(Action handler in Handler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    HandlerWrapper(handler, false);
                }
                foreach (Action handler in UIHandler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    HandlerWrapper(handler, true);
                }
            }
        }

        public override string ToString() {
            return $"({EventListenerObjName}.notify('{EventName}'));";
        }

        public event Action Handler;

        public event Action UIHandler;

        public void Dispose() {
            UnderlyingListener.NotificationReceived -= HandleEvent;
        }
    }
}
