using System;
using System.Linq;

namespace WebViewControl {
    
    partial class WebView {

        public class JavascriptException : Exception {

            internal const string AtSeparator = "   at ";

            private readonly string[] jsStack;

            public JavascriptException(string message, string[] stack)
            : base(message, null) {
                jsStack = stack;
            }

            public JavascriptException(string name, string message, string[] stack)
            : base((string.IsNullOrEmpty(name) ? "" : name + ": ") + message, null) {
                jsStack = stack;
            }

            public override string StackTrace {
                get { return string.Join(Environment.NewLine, jsStack.Select(l => l).Concat(new[] { base.StackTrace })); }
            }

            public override string ToString() {
                return GetType().FullName + ": " + Message + Environment.NewLine + StackTrace;
            }
        }
    }
}
