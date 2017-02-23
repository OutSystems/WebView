using System;

namespace WebViewControl {
    
    partial class WebView {

        public class JavascriptException : Exception {
            internal JavascriptException(string message) : base(message) {
            }
        }
    }
}
