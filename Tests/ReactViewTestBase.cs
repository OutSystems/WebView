using System;
using NUnit.Framework;

namespace Tests {

    public class ReactViewTestBase : TestBase<TestReactView> {

        protected override void InitializeView() {
            var initialized = false;
            TargetView.Ready += () => initialized = true;
            WaitFor(() => initialized, TimeSpan.FromSeconds(10), "view initialized");
        }

        [Test(Description = "Test loading a simple react component")]
        public void SimpleComponentIsLoaded() {
            Assert.Pass(); // reaching this point means success
        }

        [Test(Description = "Test properties are injected in react component and root object is exposed")]
        public void PropertiesAreInjected() {
            var eventCalled = false;
            TargetView.Event += (args) => eventCalled = true;
            TargetView.ExecuteMethodOnRoot("callEvent");
            WaitFor(() => eventCalled, TimeSpan.FromSeconds(10), "event call");
        }

        [Test(Description = "Test disposing a react view does not hang")]
        public void DisposeDoesNotHang() {
            var disposed = false;
            TargetView.Event += (args) => {
                TargetView.Dispose();
                disposed = true;
            };

            TargetView.ExecuteMethodOnRoot("callEvent");

            WaitFor(() => disposed, TimeSpan.FromSeconds(10), "view disposed");
        }

        [Test(Description = "Tests stylesheets get loaded")]
        public void StylesheetsAreLoaded() {
            string stylesheet = null;
            TargetView.Event += (args) => {
                stylesheet = args;
            };

            TargetView.ExecuteMethodOnRoot("checkStyleSheetLoaded");

            WaitFor(() => stylesheet != null, TimeSpan.FromSeconds(10), "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".foo"));
        }
    }
}
