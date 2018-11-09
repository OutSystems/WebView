using System;
using System.Linq;
using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        public class JavascriptException : Exception {

            private readonly JavascriptStackFrame[] jsStack;

            internal JavascriptException(string message, JavascriptStackFrame[] stack = null)
            : base(message, null) {
                jsStack = stack ?? new JavascriptStackFrame[0];
            }

            internal JavascriptException(string name, string message, JavascriptStackFrame[] stack = null)
            : base((string.IsNullOrEmpty(name) ? "" : name + ": ") + message, null) {
                jsStack = stack ?? new JavascriptStackFrame[0];
            }

            public override string StackTrace {
                get { return string.Join(Environment.NewLine, jsStack.Select(FormatStackFrame).Concat(new[] { base.StackTrace })); }
            }

            private static string FormatStackFrame(JavascriptStackFrame frame) {
                var functionName = string.IsNullOrEmpty(frame.FunctionName) ? "<anonymous>" : frame.FunctionName;
                var location = string.IsNullOrEmpty(frame.SourceName) ? "" : ($" in {frame.SourceName}:line {frame.LineNumber} {frame.ColumnNumber}");
                return $"   at {functionName}{location}";
            }

            public override string ToString() {
                return GetType().FullName + ": " + Message + Environment.NewLine + StackTrace;
            }
        }
    }
}
