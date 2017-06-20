using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace Tests {

    [SetUpFixture]
    class Setup {

        [OneTimeTearDown]
        public void RunAfterAnyTests() {
            if (TestContext.CurrentContext.Result.FailCount == 0 &&
                TestContext.CurrentContext.Result.InconclusiveCount == 0 &&
                TestContext.CurrentContext.Result.SkipCount == 0 &&
                TestContext.CurrentContext.Result.WarningCount == 0) {

                // kill the process otherwise will hang (due to cef), but only if everything passed
                ThreadPool.QueueUserWorkItem(_ => {
                    Thread.Sleep(2000);
                    Process.GetCurrentProcess().Kill();
                });
            }
        }
    }
}
