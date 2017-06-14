using NUnit.Framework;

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
                for (var i = 0; i < 10000; i++) {
                    TargetWebView.ExecuteScript($"x += '{i},'");
                    expectedResult += i + ",";
                }
                var result = TargetWebView.EvaluateScript<string>("x");
                Assert.AreEqual(expectedResult, result);
            } finally {
                TargetWebView.EvaluateScript<bool>("delete x");
            }
        }

        [Test(Description = "Evaluation of complex objects returns the expected results")]
        public void ComplexObjectsEvaluation() {
            var result = TargetWebView.EvaluateScript<TestObject>(" { Name: 'Snows', Age: 32, Parent: { Name: 'Snows Parent', Age: 60 } }");
            Assert.IsNotNull(result);
            Assert.AreEqual("Snows", result.Name);
            Assert.AreEqual(32, result.Age);
            Assert.IsNotNull(result.Parent);
            Assert.AreEqual("Snows Parent", result.Parent.Name);
            Assert.AreEqual(60, result.Parent.Age);
        }
    }
}
