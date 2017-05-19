using System;

namespace WebViewControl {

    public class RenderProcessTerminatedException : Exception {

        internal RenderProcessTerminatedException(string message, bool wasKilled) : base(message) {
            WasKilled = wasKilled;
        }

        public bool WasKilled { get; set; }
    }
}
