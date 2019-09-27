using System;

namespace WebViewControl {
    public class RenderProcessException : Exception {

        internal RenderProcessException(string exceptionType, string message, string stackTrace) : base(exceptionType + ": " + message) {
            StackTrace = stackTrace;
        }

        public override string StackTrace { get; }
    }
}
