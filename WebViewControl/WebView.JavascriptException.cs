using System;
using System.Collections.Generic;
using System.Linq;

namespace WebViewControl {

    partial class WebView {

        public class JavascriptException : Exception {

            private JavascriptStackFrame[] JsStack { get; }

            internal JavascriptException(string message, IEnumerable<JavascriptStackFrame> stack = null)
            : base(message, null) {
                JsStack = stack?.ToArray() ?? new JavascriptStackFrame[0];
            }

            internal JavascriptException(string name, string message, IEnumerable<JavascriptStackFrame> stack = null)
            : this((string.IsNullOrEmpty(name) ? "" : name + ": ") + message, stack) {
            }

            public override string StackTrace {
                get { return string.Join(Environment.NewLine, JsStack.Select(FormatStackFrame).Concat(new[] { base.StackTrace })); }
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
