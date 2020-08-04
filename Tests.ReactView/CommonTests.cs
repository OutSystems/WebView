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
                var eventCalled = await taskCompletionSource.Task;
                Assert.IsTrue(eventCalled, "Event 'callEvent' was not called!");
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
                var eventCalled = await taskCompletionSource.Task;

                Assert.IsTrue(eventCalled, "View was not disposed!");
            });
        }

        [Test(Description = "Tests stylesheets get loaded")]
        public async Task StylesheetsAreLoaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<string>();

                TargetView.Event += (args) => {
                    taskCompletionSource.SetResult(args);
                };

                TargetView.ExecuteMethod("checkStyleSheetLoaded", "1");
                var stylesheet = await taskCompletionSource.Task;

                Assert.IsTrue(stylesheet.Contains(".foo"));
                Assert.IsTrue(stylesheet.Contains(".baz")); // from dependency
            });
        }

        [Test(Description = "Events are not handled in the Dispatcher thread")]
        public async Task EventsAreNotHandledInDispatcherThread() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.Event += delegate {
                    taskCompletionSource.SetResult(Dispatcher.UIThread.CheckAccess());
                };

                TargetView.ExecuteMethod("callEvent");
                var canAccessDispatcher = await taskCompletionSource.Task;

                Assert.IsFalse(canAccessDispatcher, "Can access dispatcher!");
            });
        }

        [Test(Description = "Custom requests handler is called in another thread")]
        public async Task CustomRequestsAreHandledByAnotherThread() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<int>();
                var mainThread = Thread.CurrentThread.ManagedThreadId;

                TargetView.CustomResourceRequested += delegate {
                    taskCompletionSource.SetResult(Thread.CurrentThread.ManagedThreadId);
                    return null;
                };

                TargetView.ExecuteMethod("loadCustomResource", "custom://resource//test.png");
                var customResourceRequestThread = await taskCompletionSource.Task;

                Assert.AreNotEqual(mainThread, customResourceRequestThread, "Custom resource request thread should be different from main thread");
            });
        }

        [Test(Description = "Tests view ready event is dispatched.")]
        public async Task ViewReadyEventIsDispatched() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                TargetView.Event += (args) => {
                    taskCompletionSource.SetResult(args == "ViewReadyTrigger");
                };

                TargetView.ExecuteMethod("checkViewReady");
                var viewIsReady = await taskCompletionSource.Task;

                Assert.IsTrue(viewIsReady, "View is not ready!");
            });
        }
    }
}
