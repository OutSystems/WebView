using System;

namespace WebViewControl {

    public class RenderProcessCrashedException : Exception {

        internal RenderProcessCrashedException(string message) : base(message) {
        }
    }

    public class RenderProcessKilledException : Exception {

        internal RenderProcessKilledException(string message) : base(message) {
        }
    }
}
