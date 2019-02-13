using System;

namespace WebViewControl {

    public class UnhandledAsyncExceptionEventArgs {

        public UnhandledAsyncExceptionEventArgs(Exception e) {
            Exception = e;
        }

        public Exception Exception { get; private set; }
        public bool Handled { get; set; }
    }
}
