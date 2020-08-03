using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests.ReactView {

    public class CommonTests : ReactViewTestBase {

        [Test(Description = "Test loading a simple react component")]
        public void SimpleComponentIsLoaded() {
            // reaching this point means success
        }

        [Test(Description = "Test properties are injected in react component and root object is exposed")]
        public async Task PropertiesAreInjected() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.Event += delegate {
                    taskCompletionSource.SetResult(true);
                };
                TargetView.ExecuteMethod("callEvent");
                await taskCompletionSource.Task;
                Assert.IsTrue(taskCompletionSource.Task.Result, "Event 'callEvent' was not called!");
            });
        }

        [Test(Description = "Test disposing a react view does not hang")]
        public async Task DisposeDoesNotHang() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.Event += delegate {
                    Dispatcher.UIThread.InvokeAsync(() => {
                        TargetView.Dispose();
                        taskCompletionSource.SetResult(true);
                    }).Wait();
                };

                TargetView.ExecuteMethod("callEvent");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "View was not disposed!");
            });
        }

        [Test(Description = "Tests stylesheets get loaded")]
        public async Task StylesheetsAreLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                string stylesheet = null;

                TargetView.Event += (args) => {
                    stylesheet = args;
                    taskCompletionSource.SetResult(true);
                };

                TargetView.ExecuteMethod("checkStyleSheetLoaded", "1");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Stylesheet was not loaded!");
                Assert.IsTrue(stylesheet.Contains(".foo"));
                Assert.IsTrue(stylesheet.Contains(".baz")); // from dependency
            });
        }

        [Test(Description = "Events are not handled in the Dispatcher thread")]
        public async Task EventsAreNotHandledInDispatcherThread() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                bool? canAccessDispatcher = null;
                TargetView.Event += (args) => {
                    canAccessDispatcher = Dispatcher.UIThread.CheckAccess();
                    taskCompletionSource.SetResult(true);
                };

                TargetView.ExecuteMethod("callEvent");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Event was not called!");
                Assert.IsFalse(canAccessDispatcher, "Can access dispatcher");
            });
        }

        [Test(Description = "Custom requests handler is called in another thread")]
        public async Task CustomRequestsAreHandledByAnotherThread() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var mainThread = Thread.CurrentThread.ManagedThreadId;
                var customResourceRequestThread = -1;

                TargetView.CustomResourceRequested += delegate {
                    customResourceRequestThread = Thread.CurrentThread.ManagedThreadId;
                    taskCompletionSource.SetResult(true);
                    return null;
                };

                TargetView.ExecuteMethod("loadCustomResource", "custom://resource//test.png");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Request handler was not called!");
                Assert.AreNotEqual(mainThread, customResourceRequestThread, "Custom resource request thread should be different from main thread");

            });
        }

        [Test(Description = "Tests view ready event is dispatched.")]
        public async Task ViewReadyEventIsDispatched() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var viewIsReady = false;
                TargetView.Event += (args) => {
                    viewIsReady = args == "ViewReadyTrigger";
                    taskCompletionSource.SetResult(true);
                };

                TargetView.ExecuteMethod("checkViewReady");
                await taskCompletionSource.Task;

                Assert.IsTrue(taskCompletionSource.Task.Result, "Event was not called!");
                Assert.IsTrue(viewIsReady, "View is not ready!");
            });
        }
    }
}
