using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class IsolatedJavascriptEvaluation : TestBase {

        protected override bool ReuseWebView {
            get { return false; }
        }

        protected override bool WaitReady {
            get { return false; }
        }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public void JavascriptEngineInitializationTimeout() {
            LoadAndWaitReady("<html><body></body></html>");
            var exception = Assert.Throws<WebView.JavascriptException>(() => TargetWebView.EvaluateScript<int>("1", TimeSpan.FromSeconds(1)));
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Message.Contains("not initialized"));
        }
    }
}