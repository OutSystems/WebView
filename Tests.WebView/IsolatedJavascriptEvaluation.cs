using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NUnit.Framework;
using static WebViewControl.WebView;

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

                var exception = await Assertions.AssertThrows<JavascriptException>(async () => await TargetView.EvaluateScript<int>("1", timeout: TimeSpan.FromMilliseconds(0)));
                Assert.IsNotNull(exception);
                StringAssert.Contains("Timeout", exception.Message);
            });
        }

        [Test(Description = "Evaluation works after engine is initialized")]
        public async Task EvaluateAfterInitialization() {
            await Run(async () => {
                await Load("<html><script></script><body>1</body></html>");
                await Load("<html><script></script><body>2</body></html>");
                var result = await TargetView.EvaluateScript<int>("1", timeout: TimeSpan.FromSeconds(30));
                Assert.AreEqual(result, 1);
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
                RegisterJavascriptObject(DotNetObject, functionToCall, Interceptor);

                var script = $"{DotNetObject}.invoke();";
                await RunScript(script);

                var functionCalled = await taskCompletionSource.Task;

                Assert.IsTrue(functionCalled);
                Assert.IsTrue(interceptorCalled);
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
                RegisterJavascriptObject(DotNetObject, functionToCall);

                var script = $"{DotNetObject}.invoke(null, ['hello', null, 'world']);";
                await RunScript(script);

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

                RegisterJavascriptObject(DotNetObject, functionToCall);
                RegisterJavascriptObject(DotNetSetResult, setResult);

                var script = $"var result = await {DotNetObject}.invoke(); {DotNetSetResult}.invoke(result);";
                await RunScript(script);

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

                var taskCompletionSourceFunction = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var taskCompletionSourceDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Func<int> functionToCall = () => {
                    TargetView.Dispose();
                    var disposeRan = taskCompletionSourceDispose.Task.IsCompleted;
                    taskCompletionSourceFunction.SetResult(disposeRan);
                    return 1;
                };

                TargetView.RegisterJavascriptObject(DotNetObject, functionToCall);
                await Load($"<html><script>function test() {{ {DotNetObject}.invoke(); while(true); return 1; }}</script><body></body></html>");

                TargetView.Disposed += () => taskCompletionSourceDispose.SetResult(true);

                var result = await TargetView.EvaluateScriptFunction<int>("test");
                Assert.AreEqual(0, result, "Script evaluation should be cancelled and default value returned");

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

                var result = await TargetView.EvaluateScriptFunction<int>("test");

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

                var x = await TargetView.EvaluateScript<int>("x", "");
                var y = await TargetView.EvaluateScript<int>("test.y", "");
                Assert.AreEqual(1, x);
                Assert.AreEqual(2, y);
            });
        }
    }
}