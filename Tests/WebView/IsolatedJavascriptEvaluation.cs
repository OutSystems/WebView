using System;
using NUnit.Framework;

namespace Tests.WebView {

    [Ignore("Needs browser setup")]
    public class IsolatedJavascriptEvaluation : WebViewTestBase {

        protected override void InitializeView() { }

        protected override void AfterInitializeView() { }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public void JavascriptEngineInitializationTimeout() {
            //LoadAndWaitReady("<html><body></body></html>");
            var exception = Assert.Throws<WebViewControl.WebView.JavascriptException>(() => TargetView.EvaluateScript<int>("1", timeout: TimeSpan.FromSeconds(1)));
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

            WaitFor(() => functionCalled, DefaultTimeout);
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

            WaitFor(() => canAccessDispatcher != null, DefaultTimeout);
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

            WaitFor(() => functionCalled, DefaultTimeout);
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

            WaitFor(() => functionCalled, DefaultTimeout);
            Assert.IsTrue(functionCalled);
            Assert.AreEqual(null, obtainedArg1);
            Assert.That(new[] { "hello", null, "world" }, Is.EquivalentTo(obtainedArg2));
        }

        [Test(Description = ".Net Method returned objects serialization")]
        public void RegisteredJsObjectReturnObjectSerialization() {
            const string DotNetObject = "DotNetObject";
            const string DotNetSetResult = "DotNetSetResult";

            TestObject result = null;
            var testObject = new TestObject() {
                Age = 33,
                Kind = Kind.B,
                Name = "John",
                Parent = new TestObject() {
                    Name = "John Parent",
                    Age = 66,
                    Kind = Kind.C
                }
            };

            Func<TestObject> functionToCall = () => testObject;
            Action<TestObject> setResult = (r) => result = r;

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
            TargetView.RegisterJavascriptObject(DotNetSetResult, setResult);
            LoadAndWaitReady($"<html><script>(async function test() {{ var result = await {DotNetObject}.invoke(); {DotNetSetResult}.invoke(result); }})()</script><body></body></html>");

            WaitFor(() => result != null, DefaultTimeout);
            Assert.IsNotNull(result);
            Assert.AreEqual(testObject.Name, result.Name);
            Assert.AreEqual(testObject.Age, result.Age);
            Assert.AreEqual(testObject.Kind, result.Kind);
            Assert.IsNotNull(result.Parent);
            Assert.AreEqual(testObject.Parent.Name, result.Parent.Name);
            Assert.AreEqual(testObject.Parent.Age, result.Parent.Age);
            Assert.AreEqual(testObject.Parent.Kind, result.Parent.Kind);
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
            LoadAndWaitReady($"<html><script>function test() {{ {DotNetObject}.invoke(); return 1; }}</script><body></body></html>");
            TargetView.Disposed += () => disposeCalled = true;

            var result = TargetView.EvaluateScriptFunction<int>("test");
            WaitFor(() => functionCalled, DefaultTimeout);

            WaitFor(() => disposeCalled, DefaultTimeout);

            Assert.IsTrue(disposeCalled);
        }

        [Test(Description = "Javascript evaluation returns default values after webview is disposed")]
        public void JsEvaluationReturnsDefaultValuesAfterWebViewDispose() {
            var disposeCalled = false;
            LoadAndWaitReady("<html><script>function test() { return 1; }</script><body></body></html>");
            TargetView.Disposed += () => disposeCalled = true;
            TargetView.Dispose();

            WaitFor(() => disposeCalled, DefaultTimeout);

            var result = TargetView.EvaluateScriptFunction<int>("test");

            Assert.IsTrue(disposeCalled);
            Assert.AreEqual(result, 0);
        }

        [Test(Description = "Evaluation runs successfully on an iframe")]
        public void JavascriptEvaluationOnIframe() {
            LoadAndWaitReady(
                "<html>" +
                "<body>" +
                "<script>" +
                "var x = 1;" +
                "</script>" +
                "<iframe name='test' srcdoc='<html><body><script>var y = 2;</script></body></html>'></iframe>" +
                "</body>" + 
                "</html>"
            );
            var x = TargetView.EvaluateScript<int>("x", "");
            var y = TargetView.EvaluateScript<int>("y", "test");
            Assert.AreEqual(1, x);
            Assert.AreEqual(2, y);
        }
    }
}