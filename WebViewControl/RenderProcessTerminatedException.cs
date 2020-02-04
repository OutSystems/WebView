using System;

namespace WebViewControl {

    public class RenderProcessCrashedException : Exception {

        internal RenderProcessCrashedException(string message) : base(message) {
        }
    }

    public class RenderProcessKilledException : Exception {

        private readonly bool webViewDisposing;

        internal RenderProcessKilledException(string message, bool webViewDisposing = false) : base(message) {
            this.webViewDisposing = webViewDisposing;
        }

        public bool IsWebViewDisposing() {
            return webViewDisposing;
        }
    }
}
