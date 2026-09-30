using System;


namespace WebViewControl {
    public class ConsoleMessageEventArgs : EventArgs {
        public ConsoleMessageEventArgs(ELogSeverity level, string message, string source, int line) {
            Level = level;
            Message = message;
            Source = source;
            Line = line;
        }

        public ELogSeverity Level { get; }

        public string Message { get; }

        public string Source { get; }

        public int Line { get; }

        public bool OutputToConsole { get; set; } = true;
    }
}
