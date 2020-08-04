using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests.WebView {

    public class IsolatedJavascriptEvaluation : WebViewTestBase {

        protected override void InitializeView() => base.InitializeView();

        protected override Task AfterInitializeView() {
            return Task.CompletedTask;
        }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public async Task JavascriptEngineInitializationTimeout() {
            await Run(async () => {
                await Load("<html><body></body></html>");

                var exception = Assert.Throws<WebViewControl.WebView.JavascriptException>(() => TargetView.EvaluateScript<int>("1", timeout: TimeSpan.FromMilliseconds(10)));
                Assert.IsNotNull(exception);
                StringAssert.Contains("Timeout", exception.Message);
            });
        }

        [Test(Description = "Method interception function is called")]
        public async Task RegisteredJsObjectMethodInterception() {
            await Run(async () => {
                const string DotNetObject = "DotNetObject";
                var interceptorCalled = false;
                var taskCompletionSource = new TaskCompletionSource<bool>();
                Func<int> functionToCall = () => {
                    taskCompletionSource.SetResult(true);
                    return 10;
                };
                object Interceptor(Func<object> originalFunc) {
                    interceptorCalled = true;
                    return originalFunc();
                }
                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, Interceptor);
                await Load($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");
                var functionCalled = await taskCompletionSource.Task;

                Assert.IsTrue(functionCalled);
                Assert.IsTrue(interceptorCalled);
            });
        }

        [Test(Description = "Registered object methods are called in Dispatcher thread")]
        public async Task RegisteredJsObjectMethodExecutesInDispatcherThread() {
            const string DotNetObject = "DotNetObject";
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Func<int> functionToCall = () => {
                taskCompletionSource.SetResult(Dispatcher.UIThread.CheckAccess());
                return 10;
            };
            TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
            await Load($"<html><script>{DotNetObject}.invoke();</script><body></body></html>");
            var canAccessDispatcher = await taskCompletionSource.Task;

            Assert.IsTrue(canAccessDispatcher);
        }

        [Test(Description = "Registered object methods when called in Dispatcher thread do not block")]
        public async Task RegisteredJsObjectMethodExecutesInDispatcherThreadWithoutBlocking() {
            await Run(async () => {
                const string DotNetObject = "DotNetObject";
                var taskCompletionSource = new TaskCompletionSource<bool>();

                Func<int> functionToCall = () => {
                    TargetView.EvaluateScript<int>("1+1");
                    taskCompletionSource.SetResult(true);
                    return 1;
                };

                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
                await Load("<html><script>function test() { DotNetObject.invoke(); return 1; }</script><body></body></html>");

                var result = TargetView.EvaluateScriptFunction<int>("test");
                await taskCompletionSource.Task;
                Assert.AreEqual(1, result);
            });
        }

        [Test(Description = ".Net Method params serialization works with nulls")]
        public async Task RegisteredJsObjectMethodNullParamsSerialization() {
            await Run(async () => {
                const string DotNetObject = "DotNetObject";
                var taskCompletionSource = new TaskCompletionSource<Tuple<string, string[]>>();

                Action<string, string[]> functionToCall = (string arg1, string[] arg2) => {
                    taskCompletionSource.SetResult(new Tuple<string, string[]>(arg1, arg2));
                };
                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
                await Load($"<html><script>{DotNetObject}.invoke(null, ['hello', null, 'world']);</script><body></body></html>");

                var obtainedArgs = await taskCompletionSource.Task;
                Assert.AreEqual(null, obtainedArgs.Item1);
                Assert.That(new[] { "hello", null, "world" }, Is.EquivalentTo(obtainedArgs.Item2));
            });
        }

        [Test(Description = ".Net Method returned objects serialization")]
        public async Task RegisteredJsObjectReturnObjectSerialization() {
            await Run(async () => {
                const string DotNetObject = "DotNetObject";
                const string DotNetSetResult = "DotNetSetResult";

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
                var taskCompletionSource = new TaskCompletionSource<TestObject>();

                Func<TestObject> functionToCall = () => testObject;
                Action<TestObject> setResult = (r) => {
                    taskCompletionSource.SetResult(r);
                };

                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
                TargetView.RegisterJavascriptObject(DotNetSetResult, setResult);
                await Load($"<html><script>(async function test() {{ var result = await {DotNetObject}.invoke(); {DotNetSetResult}.invoke(result); }})()</script><body></body></html>");

                var result = await taskCompletionSource.Task;
                Assert.IsNotNull(result);
                Assert.AreEqual(testObject.Name, result.Name);
                Assert.AreEqual(testObject.Age, result.Age);
                Assert.AreEqual(testObject.Kind, result.Kind);
                Assert.IsNotNull(result.Parent);
                Assert.AreEqual(testObject.Parent.Name, result.Parent.Name);
                Assert.AreEqual(testObject.Parent.Age, result.Parent.Age);
                Assert.AreEqual(testObject.Parent.Kind, result.Parent.Kind);
            });
        }

        [Test(Description = "Dispose is scheduled when there are js pending calls")]
        public async Task WebViewDisposeDoesNotBlockWhenHasPendingJSCalls() {
            await Run(async () => {
                const string DotNetObject = "DotNetObject";

                var taskCompletionSourceFunction = new TaskCompletionSource<bool>();
                var taskCompletionSourceDispose = new TaskCompletionSource<bool>();

                Func<int> functionToCall = () => {
                    TargetView.Dispose();
                    var disposeRan = taskCompletionSourceDispose.Task.IsCompleted;
                    taskCompletionSourceFunction.SetResult(disposeRan);
                    return 1;
                };

                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall, executeCallsInUI: true);
                await Load($"<html><script>function test() {{ {DotNetObject}.invoke(); return 1; }}</script><body></body></html>");

                TargetView.Disposed += () => taskCompletionSourceDispose.SetResult(true);

                TargetView.EvaluateScriptFunction<int>("test");

                var disposed = await taskCompletionSourceFunction.Task;
                Assert.IsFalse(disposed, "Dispose should have been scheduled");

                disposed = await taskCompletionSourceDispose.Task;
                Assert.IsTrue(disposed);
            });
        }

        [Test(Description = "Javascript evaluation returns default values after webview is disposed")]
        public async Task JsEvaluationReturnsDefaultValuesAfterWebViewDispose() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await Load("<html><script>function test() { return 1; }</script><body></body></html>");

                TargetView.Disposed += () => {
                    taskCompletionSource.SetResult(true);
                };
                TargetView.Dispose();

                var disposeCalled = await taskCompletionSource.Task;

                var result = TargetView.EvaluateScriptFunction<int>("test");

                Assert.IsTrue(disposeCalled);
                Assert.AreEqual(result, 0);
            });
        }

        [Test(Description = "Evaluation runs successfully on an iframe")]
        public async Task JavascriptEvaluationOnIframe() {
            await Run(async () => {
                await Load(
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
                var y = TargetView.EvaluateScript<int>("test.y", "");
                Assert.AreEqual(1, x);
                Assert.AreEqual(2, y);
            });
        }
    }
}