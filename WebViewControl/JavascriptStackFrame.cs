namespace WebViewControl {
    internal class JavascriptStackFrame {
        public string FunctionName { get; private set; }
        public string SourceName { get; private set; }
        public int LineNumber { get; private set; }
        public int ColumnNumber { get; private set; }
    }
}