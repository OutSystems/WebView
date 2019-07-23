using System;

namespace WebViewControl {

    public class UnhandledAsyncExceptionEventArgs {

        public UnhandledAsyncExceptionEventArgs(Exception e, string frameName) {
            Exception = e;
            FrameName = frameName;
        }

        public Exception Exception { get; private set; }

        public string FrameName { get; private set; }

        public bool Handled { get; set; }
    }
}
