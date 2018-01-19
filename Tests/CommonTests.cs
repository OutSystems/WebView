using System;
using NUnit.Framework;

namespace Tests {

    public class CommonTests : WebViewTestBase {

        [Test(Description = "Attached listeners are called")]
        public void ListenersAreCalled() {
            var listenerCalled = false;
            var listener = TargetView.AttachListener("event_name", () => listenerCalled = true);
            LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");
            WaitFor(() => listenerCalled);
            Assert.IsTrue(listenerCalled);
        }

        [Test(Description = "Attached listeners are called in Dispatcher thread")]
        public void ListenersAreCalledInDispatcherThread() {
            bool? canAccessDispatcher = null;
            var listener = TargetView.AttachListener("event_name", () => canAccessDispatcher = TargetView.Dispatcher.CheckAccess(), executeInUI:true);
            LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");
            WaitFor(() => canAccessDispatcher != null);
            Assert.IsTrue(canAccessDispatcher);
        }

        [Test(Description = "Unhandled Exception event is called when an async unhandled error occurs inside a listener")]
        public void UnhandledExceptionEventIsCalledOnListenerError() {
            const string ExceptionMessage = "hey";

            Exception exception = null;

            WithUnhandledExceptionHandling(() => {
                var listener = TargetView.AttachListener("event_name", () => throw new Exception(ExceptionMessage));

                LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");

                WaitFor(() => exception != null);
                Assert.IsTrue(exception.Message.Contains(ExceptionMessage));
            }, 
            e => {
                exception = e;
                return true;
            });
        }

        [Test(Description = "Before navigate hook is called")]
        public void BeforeNavigateHookCalled() {
            var beforeNavigatedCalled = false;
            TargetView.BeforeNavigate += (request) => {
                request.Cancel();
                beforeNavigatedCalled = true;
            };
            TargetView.Address = "https://www.google.com";
            WaitFor(() => beforeNavigatedCalled);
            Assert.IsTrue(beforeNavigatedCalled);
        }
    }
}
