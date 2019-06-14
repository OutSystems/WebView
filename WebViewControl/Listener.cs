using System;
using System.Linq;

namespace WebViewControl {

    public delegate void ListenerEventHandler(params object[] args);

    public class Listener : IDisposable {

        internal const string EventListenerObjName = "__WebviewListener__";

        private string EventName { get; }
        private Action<ListenerEventHandler, object[], bool> HandlerWrapper { get; }
        private BrowserObjectListener UnderlyingListener { get; }

        internal Listener(string eventName, Action<ListenerEventHandler, object[], bool> handlerWrapper, BrowserObjectListener underlyingListener) {
            EventName = eventName;
            HandlerWrapper = handlerWrapper;
            UnderlyingListener = underlyingListener;

            UnderlyingListener.NotificationReceived += HandleEvent;
        }
        
        private void HandleEvent(string eventName, object[] args) {
            if (EventName == eventName) {
                foreach(ListenerEventHandler handler in Handler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    HandlerWrapper(handler, args, false);
                }
                foreach (ListenerEventHandler handler in UIHandler?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {
                    HandlerWrapper(handler, args, true);
                }
            }
        }

        public override string ToString() {
            return $"({EventListenerObjName}.notify('{EventName}'));";
        }

        public event ListenerEventHandler Handler;

        public event ListenerEventHandler UIHandler;

        public void Dispose() {
            UnderlyingListener.NotificationReceived -= HandleEvent;
        }
    }
}
