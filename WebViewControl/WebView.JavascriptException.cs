using System;
using System.Collections.Generic;
using System.Linq;
using Xilium.CefGlue.Common.Events;

namespace WebViewControl {

    partial class WebView {

        public class JavascriptException : Exception {

            private JavascriptStackFrame[] JsStack { get; }
            private string InnerStack { get; }

            internal JavascriptException(string message, IEnumerable<JavascriptStackFrame> stack = null, string innerStackTrace = null)
            : base(message, null) {
                JsStack = stack?.ToArray() ?? new JavascriptStackFrame[0];
                InnerStack = innerStackTrace;
            }

            internal JavascriptException(string name, string message, IEnumerable<JavascriptStackFrame> stack = null, string baseStackTrace = null)
            : this((string.IsNullOrEmpty(name) ? "" : name + ": ") + message, stack, baseStackTrace) {
            }

            public override string StackTrace {
                get { return string.Join(Environment.NewLine, JsStack.Select(FormatStackFrame).Concat(new[] { BaseStackTrace })); }
            }

            private string BaseStackTrace => (InnerStack != null ? InnerStack + Environment.NewLine : "") + base.StackTrace;

            private static string FormatStackFrame(JavascriptStackFrame frame) {
                var functionName = string.IsNullOrEmpty(frame.FunctionName) ? "<anonymous>" : frame.FunctionName;
                var location = string.IsNullOrEmpty(frame.ScriptNameOrSourceUrl) ? "" : ($" in {frame.ScriptNameOrSourceUrl}:line {frame.LineNumber} {frame.Column}");
                return $"   at {functionName}{location}";
            }

            public override string ToString() {
                return GetType().FullName + ": " + Message + Environment.NewLine + StackTrace;
            }
        }
    }
}
