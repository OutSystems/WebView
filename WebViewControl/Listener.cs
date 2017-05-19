using System;

namespace WebViewControl {

    public class Listener {

        internal const string EventListenerObjName = "__WebviewListener__";

        private readonly string eventName;

        internal Listener(string eventName, Action<string> handler) {
            this.eventName = eventName;
            Handler = handler;
        }

        internal Action<string> Handler { get; private set; }

        public override string ToString() {
            return $"({EventListenerObjName}.notify('{eventName}'));";
        }
    }
}
