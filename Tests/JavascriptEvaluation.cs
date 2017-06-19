using System;
using System.Linq;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class JavascriptEvaluation : TestBase {

        class TestObject {
            public string Name;
            public int Age;
            public TestObject Parent;
        }

        [Test(Description = "A simple script evaluates correctly")]
        public void EvaluateSimpleScript() {
            var result = TargetWebView.EvaluateScript<int>("2+1");
            Assert.AreEqual(3, result);
        }

        [Test(Description = "The order of the executed scripts is respected")]
        public void ExecutionOrderIsRespected() {
            try {
                TargetWebView.ExecuteScript("x = ''");
                var expectedResult = "";
                // queue 10000 scripts
                for (var i = 0; i < 10000; i++) {
                    TargetWebView.ExecuteScript($"x += '{i},'");
                    expectedResult += i + ",";
                }
                var result = TargetWebView.EvaluateScript<string>("x");
                Assert.AreEqual(expectedResult, result);

                TargetWebView.ExecuteScript("x = '-'");
                result = TargetWebView.EvaluateScript<string>("x");
                Assert.AreEqual("-", result);

            } finally {
                TargetWebView.EvaluateScript<bool>("delete x");
            }
        }

        [Test(Description = "Evaluation of complex objects returns the expected results")]
        public void ComplexObjectsEvaluation() {
            var result = TargetWebView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32, Parent: { Name: 'Snows Parent', Age: 60 } })");
            Assert.IsNotNull(result);
            Assert.AreEqual("Snows", result.Name);
            Assert.AreEqual(32, result.Age);
            Assert.IsNotNull(result.Parent);
            Assert.AreEqual("Snows Parent", result.Parent.Name);
            Assert.AreEqual(60, result.Parent.Age);
        }

        [Test(Description = "Evaluation of scripts with errors returns stack and message details")]

        public void EvaluationErrorsReturnsDetails() {
            var exception = Assert.Throws<WebView.JavascriptException>(() => TargetWebView.EvaluateScript<int>("(function foo() { (function bar() { throw new Error('ups'); })() })()"));
            Assert.AreEqual("Error: ups", exception.Message);
            var stack = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Greater(stack.Length, 2);
            Assert.True(stack.ElementAt(0).Contains("bar"));
            Assert.True(stack.ElementAt(1).Contains("foo"));
        }

        [Test(Description = "Evaluation of scripts with comments, json objects, and var declarations")]

        public void ScriptsSyntax() {
            var result = TargetWebView.EvaluateScript<int>("2+1 // some comments");
            Assert.AreEqual(3, result);

            result = TargetWebView.EvaluateScript<int>("var x = 1; 5");
            Assert.AreEqual(5, result);

            var resultObj = TargetWebView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32})");
            Assert.IsNotNull(resultObj);
        }

        [Test(Description = "Evaluation of scripts timesout after timeout elapsed")]

        public void EvaluationTimeoutIsThrown() {
            var exception = Assert.Throws<WebView.JavascriptException>(
                () => TargetWebView.EvaluateScript<int>("var start = new Date().getTime(); while((new Date().getTime() - start) < 150);",
                TimeSpan.FromMilliseconds(50)));
            Assert.True(exception.Message.Contains("Timeout"));

        }

        [Test(Description = "Evaluation of null returns empty array when result is array type")]

        public void EvaluationReturnsEmptyArraysWhenNull() {
            var result = TargetWebView.EvaluateScript<int[]>("null");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }
    }
}
