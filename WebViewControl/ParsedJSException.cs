using System;
using System.Linq;

namespace WebViewControl {

    internal class ParsedJSException : Exception {
        
        private string[] jsStack;
        private string[] messageLog;

        public ParsedJSException(string name, string message, string[] stack, string[] messages)
            : base(name + ": " + message, null) {

            jsStack = stack;
            messageLog = messages;
        }

        public override string ToString() {
            return base.ToString() + Environment.NewLine +
                (messageLog.Length == 0 ? "" : string.Format("messages: {0} -" + string.Join("{0} -", messageLog), Environment.NewLine));
        }

        public override string StackTrace {
            get {
                return string.Join(Environment.NewLine, jsStack.Concat(new[] { base.StackTrace }));
            }
        }
    }
}
