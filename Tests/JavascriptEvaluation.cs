using System;
using System.Linq;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class JavascriptEvaluation : WebViewTestBase {

        enum Kind {
            A,
            B,
            C
        }

        class TestObject {
            public string Name;
            public int Age;
            public TestObject Parent;
            public Kind Kind;
        }

        [Test(Description = "A simple script evaluates correctly")]
        public void EvaluateSimpleScript() {
            var result = TargetView.EvaluateScript<int>("2+1");
            Assert.AreEqual(3, result);
        }

        [Test(Description = "The order of the executed scripts is respected")]
        public void ExecutionOrderIsRespected() {
            try {
                TargetView.ExecuteScript("x = ''");
                var expectedResult = "";
                // queue 10000 scripts
                for (var i = 0; i < 10000; i++) {
                    TargetView.ExecuteScript($"x += '{i},'");
                    expectedResult += i + ",";
                }
                var result = TargetView.EvaluateScript<string>("x");
                Assert.AreEqual(expectedResult, result);

                TargetView.ExecuteScript("x = '-'");
                result = TargetView.EvaluateScript<string>("x");
                Assert.AreEqual("-", result);

            } finally {
                TargetView.EvaluateScript<bool>("delete x");
            }
        }

        [Test(Description = "Evaluation of complex objects returns the expected results")]
        public void ComplexObjectsEvaluation() {
            var result = TargetView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32, Parent: { Name: 'Snows Parent', Age: 60 }, Kind: 2 })");
            Assert.IsNotNull(result);
            Assert.AreEqual("Snows", result.Name);
            Assert.AreEqual(32, result.Age);
            Assert.IsNotNull(result.Parent);
            Assert.AreEqual("Snows Parent", result.Parent.Name);
            Assert.AreEqual(60, result.Parent.Age);
            Assert.AreEqual(Kind.C, result.Kind);
        }

        [Test(Description = "Evaluation of scripts with errors returns stack and message details")]
        public void EvaluationErrorsReturnsDetails() {
            var exception = Assert.Throws<WebView.JavascriptException>(() => TargetView.EvaluateScript<int>("(function foo() { (function bar() { throw new Error('ups'); })() })()"));
            Assert.AreEqual("Error: ups", exception.Message);
            var stack = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Greater(stack.Length, 2);
            Assert.True(stack.ElementAt(0).Contains("bar"));
            Assert.True(stack.ElementAt(1).Contains("foo"));
        }

        [Test(Description = "Evaluation of scripts with comments, json objects, and var declarations")]
        public void ScriptsSyntax() {
            var result = TargetView.EvaluateScript<int>("2+1 // some comments");
            Assert.AreEqual(3, result);

            result = TargetView.EvaluateScript<int>("var x = 1; 5");
            Assert.AreEqual(5, result);

            var resultObj = TargetView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32})");
            Assert.IsNotNull(resultObj);
        }

        [Test(Description = "Evaluation of scripts timesout after timeout elapsed")]
        public void EvaluationTimeoutIsThrown() {
            var exception = Assert.Throws<WebView.JavascriptException>(
                () => TargetView.EvaluateScript<int>("var start = new Date().getTime(); while((new Date().getTime() - start) < 150);",
                TimeSpan.FromMilliseconds(50)));
            Assert.True(exception.Message.Contains("Timeout"));

        }

        [Test(Description = "Evaluation of null returns empty array when result is array type")]
        public void EvaluationReturnsEmptyArraysWhenNull() {
            var result = TargetView.EvaluateScript<int[]>("null");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test(Description = "Unhandled Exception event is called when an async error occurs")]
        public void UnhandledExceptionEventIsCalled() {
            const string ExceptionMessage = "nooo";
            Exception asyncException = null;

            var failOnAsyncExceptions = FailOnAsyncExceptions;
            FailOnAsyncExceptions = false;
            try {
                TargetView.UnhandledAsyncException += (e) => asyncException = e;
                TargetView.ExecuteScript($"throw new Error('{ExceptionMessage}')");
                var result = TargetView.EvaluateScript<int>("1+1"); // force exception to occur
                Assert.NotNull(asyncException);
                Assert.IsTrue(asyncException.Message.Contains(asyncException.Message));
                Assert.AreEqual(2, result, "Result should not be affected");
            } finally {
                FailOnAsyncExceptions = failOnAsyncExceptions;
            }
        }
    }
}
