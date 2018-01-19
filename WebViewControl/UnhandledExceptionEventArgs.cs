using System;

namespace WebViewControl {

    public class UnhandledExceptionEventArgs {

        public UnhandledExceptionEventArgs(Exception e) {
            Exception = e;
        }

        public Exception Exception { get; private set; }
        public bool Handled { get; set; }
    }
}
