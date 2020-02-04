using System;

namespace WebViewControl {

    public class RenderProcessCrashedException : Exception {

        internal RenderProcessCrashedException(string message) : base(message) {
        }
    }

    public class RenderProcessKilledException : Exception {

        public bool IsWebViewDisposing { get; }

        internal RenderProcessKilledException(string message, bool webViewDisposing = false) : base(message) {
            IsWebViewDisposing = webViewDisposing;
        }
    }
}
