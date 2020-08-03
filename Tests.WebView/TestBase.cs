using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests {

    [TestFixture]
    public abstract class TestBase<T> where T : class, IDisposable, new() {

        private static object initLock = new object();
        private static bool initialized = false;

        protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);

        private Window window;
        private T view;

        protected static string CurrentTestName => TestContext.CurrentContext.Test.Name;

        protected Task Run(Func<Task> func) => Dispatcher.UIThread.InvokeAsync(func, DispatcherPriority.Background);

        protected Task Run(Action action) => Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background);

        [OneTimeSetUp]
        protected Task OneTimeSetUp() {
            if (initialized) {
                return Task.CompletedTask;
            }

            lock (initLock) {
                if (initialized) {
                    return Task.CompletedTask;
                }

                var taskCompletionSource = new TaskCompletionSource<bool>();
                var uiThread = new Thread(() => {
                    AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

                    Dispatcher.UIThread.Post(() => {
                        initialized = true;
                        taskCompletionSource.SetResult(true);
                    });
                    Dispatcher.UIThread.MainLoop(CancellationToken.None);
                });
                uiThread.IsBackground = true;
                uiThread.Start();
                return taskCompletionSource.Task;
            }
        }

        [OneTimeTearDown]
        protected async Task OneTimeTearDown() {
            await Run(() => {
                if (view != null) {
                    view.Dispose();
                }
                window.Close();
            });
        }

        [SetUp]
        protected async Task SetUp() {
            await Run(async () => {
                window = new Window {
                    Title = "Running: " + CurrentTestName
                };

                if (view == null) {
                    view = CreateView();

                    if (view != null) {
                        InitializeView();
                    }

                    window.Content = view;
                    window.Show();

                    if (view != null) {
                        await AfterInitializeView();
                    }
                }
            });
        }

        protected Window Window => window;

        protected virtual T CreateView() {
            return new T();
        }

        protected virtual void InitializeView() { }

        protected virtual Task AfterInitializeView() {
            return Task.CompletedTask;
        }

        [TearDown]
        protected async Task TearDown() {
            if (Debugger.IsAttached && TestContext.CurrentContext.Result.FailCount > 0) {
                ShowDebugConsole();
                await new TaskCompletionSource<bool>().Task;
            } else {
                await Run(() => {
                    if (view != null) {
                        view.Dispose();
                        view = null;
                    }
                    window.Content = null;
                    window.Close();
                });
            }
        }

        protected abstract void ShowDebugConsole();

        protected T TargetView {
            get { return view; }
        }

        protected bool FailOnAsyncExceptions { get; set; } = !Debugger.IsAttached;

        protected void OnUnhandledAsyncException(WebViewControl.UnhandledAsyncExceptionEventArgs e) {
            if (FailOnAsyncExceptions) {
                Dispatcher.UIThread.InvokeAsync(new Action(() => {
                    Assert.Fail("An async exception ocurred: " + e.Exception.ToString());
                }));
            }
        }


        // TODO remove the methods below

        public void WaitFor(params Task[] tasks) {
            WaitFor(DefaultTimeout, tasks);
        }

        public void WaitFor(TimeSpan timeout, params Task[] tasks) {
            var start = DateTime.Now;
            while (tasks.Any(t => !t.IsCompleted)) {
                if ((DateTime.Now - start) >= timeout) {
                    throw new TimeoutException("Timed out waiting for a task to complete!");
                }
                Dispatcher.UIThread.RunJobs(DispatcherPriority.MinValue);
                Thread.Sleep(1);
            }
        }

        public void WaitFor(Func<bool> predicate, string purpose = "") {
            WaitFor(predicate, DefaultTimeout, purpose);
        }

        public static void WaitFor(Func<bool> predicate, TimeSpan timeout, string purpose = "") {
            var start = DateTime.Now;
            while (!predicate() && (DateTime.Now - start) < timeout && Application.Current != null) {
                DoEvents();
            }
            var elapsed = DateTime.Now - start;
            if (!predicate()) {
                throw new TimeoutException("Timed out waiting for " + purpose);
            }
        }

        [DebuggerNonUserCode]
        protected static void DoEvents() {
            var task = Dispatcher.UIThread.InvokeAsync(delegate { }, DispatcherPriority.MinValue);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.MinValue);
            task.Wait();
            Thread.Sleep(1);
        }

    }
}
