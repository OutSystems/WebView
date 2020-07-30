using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests.WebView {

    public class IsolatedJavascriptEvaluation : WebViewTestBase {

        protected override void InitializeView() { }

        protected override Task AfterInitializeView() {
            return Task.CompletedTask;
        }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public void JavascriptEngineInitializationTimeout() {
            var loadTask = Load("<html><body></body></html>");
            WaitFor(loadTask);
            var exception = Assert.Throws<WebViewControl.WebView.JavascriptException>(() => TargetView.EvaluateScript<int>("1", timeout: TimeSpan.FromMilliseconds(25)));
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Message.Contains("Timeout"));
        }

        [Test(Description = "Method interception function is called")]
        public void RegisteredJsObjectMethodInterception() {
            const string DotNetObject = "DotNetObject";
            var functionCalled = false;
            var interceptorCalled = false;
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Func<int> functionToCall = () => {
                functionCalled = true;
                taskCompletionSource.SetResult(true);
                return 10;
            };
            Func<Func<object>, object> interceptor = (originalFunc) => {
                interceptorCalled = true;
                return originalFunc();
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, interceptor);
            var loadTask = Load($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");
            WaitFor(loadTask, taskCompletionSource.Task);

            Assert.IsTrue(functionCalled);
            Assert.IsTrue(interceptorCalled);
        }

        [Test(Description = "Registered object methods are called in Dispatcher thread")]
        public void RegisteredJsObjectMethodExecutesInDispatcherThread() {
            const string DotNetObject = "DotNetObject";
            bool? canAccessDispatcher = null;
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Func<int> functionToCall = () => {
                canAccessDispatcher = Dispatcher.UIThread.CheckAccess();
                taskCompletionSource.SetResult(true);
                return 10;
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            var loadTask = Load($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");
            WaitFor(loadTask, taskCompletionSource.Task);

            Assert.IsTrue(canAccessDispatcher);
        }

        // TODO Failing
        [Test(Description = "Registered object methods when called in Dispatcher thread do not block")]
        public void RegisteredJsObjectMethodExecutesInDispatcherThreadWithoutBlocking() {
            const string DotNetObject = "DotNetObject";
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Func<int> functionToCall = () => {
                TargetView.EvaluateScript<int>("1+1");
                taskCompletionSource.SetResult(true);
                return 1;
            };

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            var loadTask = Load("<html><script>function test() { DotNetObject.invoke(); return 1; }</script><body></body></html>");
            WaitFor(loadTask);

            var result = TargetView.EvaluateScriptFunction<int>("test");
            WaitFor(taskCompletionSource.Task);
            Assert.AreEqual(1, result);
        }

        [Test(Description = ".Net Method params serialization works with nulls")]
        public void RegisteredJsObjectMethodNullParamsSerialization() {
            const string DotNetObject = "DotNetObject";
            var taskCompletionSource = new TaskCompletionSource<bool>();
            string obtainedArg1 = "";
            string[] obtainedArg2 = null;
            Action<string, string[]> functionToCall = (string arg1, string[] arg2) => {
                obtainedArg1 = arg1;
                obtainedArg2 = arg2;
                taskCompletionSource.SetResult(true);
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
            var loadTask = Load($"<html><script>{DotNetObject}.invoke(null, ['hello', null, 'world']);</script><body></body></html>");

            WaitFor(loadTask, taskCompletionSource.Task);
            Assert.AreEqual(null, obtainedArg1);
            Assert.That(new[] { "hello", null, "world" }, Is.EquivalentTo(obtainedArg2));
        }

        // TODO Failing
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
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Func<TestObject> functionToCall = () => testObject;
            Action<TestObject> setResult = (r) => {
                result = r;
                taskCompletionSource.SetResult(true);
            };

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
            TargetView.RegisterJavascriptObject(DotNetSetResult, setResult);
            var loadTask = Load($"<html><script>(async function test() {{ var result = await {DotNetObject}.invoke(); {DotNetSetResult}.invoke(result); }})()</script><body></body></html>");

            WaitFor(loadTask, taskCompletionSource.Task);
            Assert.IsNotNull(result);
            Assert.AreEqual(testObject.Name, result.Name);
            Assert.AreEqual(testObject.Age, result.Age);
            Assert.AreEqual(testObject.Kind, result.Kind);
            Assert.IsNotNull(result.Parent);
            Assert.AreEqual(testObject.Parent.Name, result.Parent.Name);
            Assert.AreEqual(testObject.Parent.Age, result.Parent.Age);
            Assert.AreEqual(testObject.Parent.Kind, result.Parent.Kind);
        }

        // TODO Failing
        [Test(Description = "Dispose is scheduled when there are js pending calls")]
        public void WebViewDisposeDoesNotBlockWhenHasPendingJSCalls() {
            const string DotNetObject = "DotNetObject";

            var disposeCalled = false;
            var taskCompletionSourceFunction = new TaskCompletionSource<bool>();
            var taskCompletionSourceDispose = new TaskCompletionSource<bool>();

            Func<int> functionToCall = () => {
                TargetView.Dispose();
                Assert.IsFalse(disposeCalled); // dispose should have been scheduled
                taskCompletionSourceFunction.SetResult(true);
                return 1;
            };

            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            var loadTask = Load($"<html><script>function test() {{ {DotNetObject}.invoke(); return 1; }}</script><body></body></html>");
            TargetView.Disposed += () => {
                disposeCalled = true;
                taskCompletionSourceDispose.SetResult(true);
            };
            WaitFor(loadTask);

            var result = TargetView.EvaluateScriptFunction<int>("test");
            WaitFor(taskCompletionSourceFunction.Task, taskCompletionSourceDispose.Task);

            Assert.IsTrue(disposeCalled);
        }

        // TODO Failing
        [Test(Description = "Javascript evaluation returns default values after webview is disposed")]
        public void JsEvaluationReturnsDefaultValuesAfterWebViewDispose() {
            var disposeCalled = false;
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var loadTask = Load("<html><script>function test() { return 1; }</script><body></body></html>");
            TargetView.Disposed += () => {
                disposeCalled = true;
                taskCompletionSource.SetResult(true);
            };
            TargetView.Dispose();

            WaitFor(loadTask, taskCompletionSource.Task);

            var result = TargetView.EvaluateScriptFunction<int>("test");

            Assert.IsTrue(disposeCalled);
            Assert.AreEqual(result, 0);
        }

        // TODO Failing
        [Test(Description = "Evaluation runs successfully on an iframe")]
        public void JavascriptEvaluationOnIframe() {
            var loadTask = Load(
                "<html>" +
                "<body>" +
                "<script>" +
                "var x = 1;" +
                "</script>" +
                "<iframe name='test' srcdoc='<html><body><script>var y = 2;</script></body></html>'></iframe>" +
                "</body>" + 
                "</html>"
            );
            WaitFor(loadTask);
            var x = TargetView.EvaluateScript<int>("x", "");
            var y = TargetView.EvaluateScript<int>("y", "test");
            Assert.AreEqual(1, x);
            Assert.AreEqual(2, y);
        }
    }
}