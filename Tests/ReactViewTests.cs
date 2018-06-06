using System;
using System.Threading;
using NUnit.Framework;
using WebViewControl;

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
                TargetView.Dispatcher.Invoke((() => {
                    TargetView.Dispose();
                    disposed = true;
                }));
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

            TargetView.ExecuteMethodOnRoot("checkStyleSheetLoaded", "1");

            WaitFor(() => stylesheet != null, TimeSpan.FromSeconds(10), "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".foo"));
            Assert.IsTrue(stylesheet.Contains(".baz")); // from dependency
        }

        [Test(Description = "Events are not handled in the Dispatcher thread")]
        public void EventsAreNotHandledInDispatcherThread() {
            bool? canAccessDispatcher = null;
            TargetView.Event += (args) => {
                canAccessDispatcher = TargetView.Dispatcher.CheckAccess();
            };

            TargetView.ExecuteMethodOnRoot("callEvent");

            WaitFor(() => canAccessDispatcher != null, TimeSpan.FromSeconds(10), "event call");
            Assert.IsFalse(canAccessDispatcher, "Can access dispatcher");
        }

        [Test(Description = "Custom requests handler throws timeout exception after sometime")]
        public void CustomRequestsInterceptionTimeouts() {
            var requestHandlerCalled = false;
            TargetView.CustomResourceRequested += (req) => {
                requestHandlerCalled = true;
                Thread.Sleep(1000000);
                return null;
            };

            var originalTimeout = ReactViewRender.CustomRequestTimeout;
            try {
                ReactViewRender.CustomRequestTimeout = TimeSpan.FromMilliseconds(500);

                var exceptionThrown = false;
                WithUnhandledExceptionHandling(() => {
                    TargetView.ExecuteMethodOnRoot("loadCustomResource", "custom://webview/test.png");
                    WaitFor(() => requestHandlerCalled && exceptionThrown, TimeSpan.FromSeconds(10), "exception thrown");
                }, (e) => {
                    exceptionThrown = true;
                    return true;
                });

                Assert.IsTrue(requestHandlerCalled, "Request handler was called");
                Assert.IsTrue(exceptionThrown, "Exception was thrown");
            } finally {
                ReactViewRender.CustomRequestTimeout = originalTimeout;
            }

        }
    }
}
