using System;
using System.Threading;
using System.Windows;
using NUnit.Framework;

namespace Tests {

    public class ReactViewTests : ReactViewTestBase {

        [Test(Description = "Test loading a simple react component")]
        public void SimpleComponentIsLoaded() {
            // reaching this point means success
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

        [Test(Description = "Tests stylesheets get loaded")]
        public void EventsAreHandledInDispatcherThread() {
            int? threadId = null;
            TargetView.Event += (args) => {
                threadId = Thread.CurrentThread.ManagedThreadId;
            };

            TargetView.ExecuteMethodOnRoot("callEvent");

            WaitFor(() => threadId != null, TimeSpan.FromSeconds(10), "event call");

            Assert.AreEqual(Application.Current.Dispatcher.Thread.ManagedThreadId, threadId, "Not the UI thread");
        }
    }
}
