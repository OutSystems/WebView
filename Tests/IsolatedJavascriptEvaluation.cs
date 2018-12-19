using System;
using System.Threading;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class IsolatedJavascriptEvaluation : WebViewTestBase {

        protected override bool ReuseView {
            get { return false; }
        }

        protected override void InitializeView() { }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public void JavascriptEngineInitializationTimeout() {
            LoadAndWaitReady("<html><body></body></html>");
            var exception = Assert.Throws<WebView.JavascriptException>(() => TargetView.EvaluateScript<int>("1", TimeSpan.FromSeconds(1)));
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Message.Contains("not initialized"));
        }

        [Test(Description = "Method interception function is called")]
        public void RegisteredJsObjectMethodInterception() {
            const string DotNetObject = "DotNetObject";
            var functionCalled = false;
            var interceptorCalled = false;
            Func<int> functionToCall = () => {
                functionCalled = true;
                return 10;
            };
            Func<Func<object>, object> interceptor = (originalFunc) => {
                interceptorCalled = true;
                return originalFunc();
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, interceptor);
            LoadAndWaitReady($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");

            WaitFor(() => functionCalled, TimeSpan.FromSeconds(2));
            Assert.IsTrue(functionCalled);
            Assert.IsTrue(interceptorCalled);
        }

        [Test(Description = "Registered object methods are called in Dispatcher thread")]
        public void RegisteredJsObjectMethodExecutesInDispatcherThread() {
            const string DotNetObject = "DotNetObject";
            bool? canAccessDispatcher = null;

            Func<int> functionToCall = () => {
                canAccessDispatcher = TargetView.Dispatcher.CheckAccess();
                return 10;
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            LoadAndWaitReady($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");

            WaitFor(() => canAccessDispatcher != null, TimeSpan.FromSeconds(2));
            Assert.IsTrue(canAccessDispatcher);
        }

        [Test(Description = "Registered object methods when called in Dispatcher thread do not block")]
        public void RegisteredJsObjectMethodExecutesInDispatcherThreadWithoutBlocking() {
            const string DotNetObject = "DotNetObject";
            bool functionCalled = false;

            Func<int> functionToCall = () => {
                TargetView.EvaluateScript<int>("1+1");
                functionCalled = true;
                return 1;
            };

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            LoadAndWaitReady("<html><script>function test() { DotNetObject.invoke(); return 1; }</script><body></body></html>");

            var result = TargetView.EvaluateScriptFunction<int>("test");

            WaitFor(() => functionCalled, TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, result);
        }

        [Test(Description = ".Net Method params serialization works with nulls")]
        public void RegisteredJsObjectMethodNullParamsSerialization() {
            const string DotNetObject = "DotNetObject";
            var functionCalled = false;
            string obtainedArg1 = "";
            string[] obtainedArg2 = null;
            Action<string, string[]> functionToCall = (string arg1, string[] arg2) => {
                functionCalled = true;
                obtainedArg1 = arg1;
                obtainedArg2 = arg2;
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
            LoadAndWaitReady($"<html><script>{DotNetObject}.invoke(null, ['hello', null, 'world']);</script><body></body></html>");

            WaitFor(() => functionCalled, TimeSpan.FromSeconds(2));
            Assert.IsTrue(functionCalled);
            Assert.AreEqual(null, obtainedArg1);
            Assert.That(new[] { "hello", null, "world" }, Is.EquivalentTo(obtainedArg2));
        }

        [Test(Description = "Dispose is scheduled when there are js pending calls")]
        public void WebViewDisposeDoesNotBlockWhenHasPendingJSCalls() {
            const string DotNetObject = "DotNetObject";

            var functionCalled = false;
            var disposeCalled = false;

            Func<int> functionToCall = () => {
                functionCalled = true;
                TargetView.Dispose();
                Assert.IsFalse(disposeCalled); // dispose should have been scheduled
                return 1;
            };

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            LoadAndWaitReady("<html><script>function test() { DotNetObject.invoke(); return 1; }</script><body></body></html>");
            TargetView.Disposed += () => disposeCalled = true;

            var result = TargetView.EvaluateScriptFunction<int>("test");
            WaitFor(() => functionCalled, TimeSpan.FromSeconds(2));

            WaitFor(() => disposeCalled, TimeSpan.FromSeconds(2));

            Assert.IsTrue(disposeCalled);
        }

        [Test(Description = "")]
        public void JsEvaluationReturnsDefaultValuesAfterWebViewwDispose() {
            var disposeCalled = false;
            LoadAndWaitReady("<html><script>function test() { return 1; }</script><body></body></html>");
            TargetView.Disposed += () => disposeCalled = true;
            TargetView.Dispose();

            WaitFor(() => disposeCalled, TimeSpan.FromSeconds(2));

            var result = TargetView.EvaluateScriptFunction<int>("test");

            Assert.IsTrue(disposeCalled);
            Assert.AreEqual(result, 0);
        }
    }
}