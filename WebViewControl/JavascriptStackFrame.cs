namespace WebViewControl {

    /// <summary>
    /// A javascript call stack entry, independent of the engine that produced it.
    /// </summary>
    internal sealed class JavascriptStackFrame {

        public JavascriptStackFrame(string functionName, string scriptNameOrSourceUrl, int column, int lineNumber) {
            FunctionName = functionName;
            ScriptNameOrSourceUrl = scriptNameOrSourceUrl;
            Column = column;
            LineNumber = lineNumber;
        }

        public string FunctionName { get; }

        public string ScriptNameOrSourceUrl { get; }

        public int Column { get; }

        public int LineNumber { get; }
    }
}
