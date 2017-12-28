using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {
    
    public class ReactViewTestBase : TestBase<ReactView> {

        protected override void InitializeView() {
            var initialized = false;
            TargetView.Ready += () => initialized = true;
            TargetView.Source = "ReactViewResources/dist/TestApp";
            WaitFor(() => initialized, TimeSpan.FromSeconds(1000), "view initialized");
        }

        [Test(Description = "Test loading a simple react component")]
        public void SimpleComponentIsLoaded() {
            Assert.Pass(); // reaching this point means success
        }
    }
}
