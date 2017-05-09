using System;
using System.Linq;

namespace WebViewControl {
    
    partial class WebView {

        public class JavascriptException : Exception {

            private readonly string[] jsStack;

            public JavascriptException(string name, string message, string[] stack)
            : base(name + ": " + message, null) {

                jsStack = stack;
            }

            public override string StackTrace {
                get { return string.Join(Environment.NewLine, jsStack.Select(l => "   at " + l).Concat(new[] { base.StackTrace })); }
            }

            public override string ToString() {
                return GetType().FullName + ": " + Message + Environment.NewLine + StackTrace;
            }
        }
    }
}
