using System;
using System.Threading;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests.ReactView {

    public class CommonTests : ReactViewTestBase {

        [Test(Description = "Test loading a simple react component")]
        public void SimpleComponentIsLoaded() {
            // reaching this point means success
        }

        [Test(Description = "Test properties are injected in react component and root object is exposed")]
        public void PropertiesAreInjected() {
            var eventCalled = false;
            TargetView.Event += (args) => eventCalled = true;
            TargetView.ExecuteMethod("callEvent");
            WaitFor(() => eventCalled, TimeSpan.FromSeconds(10), "event call");
        }

        [Test(Description = "Test disposing a react view does not hang")]
        public void DisposeDoesNotHang() {
            var disposed = false;
            TargetView.Event += (args) => {
                Dispatcher.UIThread.InvokeAsync(() => {
                    TargetView.Dispose();
                    disposed = true;
                }).Wait();
            };

            TargetView.ExecuteMethod("callEvent");

            WaitFor(() => disposed, TimeSpan.FromSeconds(10), "view disposed");
        }

        [Test(Description = "Tests stylesheets get loaded")]
        public void StylesheetsAreLoaded() {
            string stylesheet = null;
            TargetView.Event += (args) => {
                stylesheet = args;
            };

            TargetView.ExecuteMethod("checkStyleSheetLoaded", "1");

            WaitFor(() => stylesheet != null, TimeSpan.FromSeconds(10), "stylesheet load");

            Assert.IsTrue(stylesheet.Contains(".foo"));
            Assert.IsTrue(stylesheet.Contains(".baz")); // from dependency
        }

        [Test(Description = "Events are not handled in the Dispatcher thread")]
        public void EventsAreNotHandledInDispatcherThread() {
            bool? canAccessDispatcher = null;
            TargetView.Event += (args) => {
                canAccessDispatcher = Dispatcher.UIThread.CheckAccess();
            };

            TargetView.ExecuteMethod("callEvent");

            WaitFor(() => canAccessDispatcher != null, TimeSpan.FromSeconds(10), "event call");
            Assert.IsFalse(canAccessDispatcher, "Can access dispatcher");
        }

        [Test(Description = "Custom requests handler is called in another thread")]
        public void CustomRequestsAreHandledByAnotherThread() {
            var requestHandlerCalled = false;
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            var customResourceRequestThread = -1;

            TargetView.CustomResourceRequested += (_, __) => {
                customResourceRequestThread = Thread.CurrentThread.ManagedThreadId;
                requestHandlerCalled = true;
                return null;
            };

            TargetView.ExecuteMethod("loadCustomResource", "custom://webview/test.png");
            WaitFor(() => requestHandlerCalled, "custom request handler called");
                
            Assert.IsTrue(requestHandlerCalled, "Request handler was called");
            Assert.AreNotEqual(mainThread, customResourceRequestThread, "custom resource request thread should be different from main thread");
        }

        [Test(Description = "Tests view ready event is dispatched.")]
        public void ViewReadyEventIsDispatched() {
            var viewIsReady = false;
            TargetView.Event += (args) => {
                viewIsReady = args == "ViewReadyTrigger";
            };

            TargetView.ExecuteMethod("checkViewReady");

            WaitFor(() => viewIsReady, "View is ready");
        }
    }
}
