using System;
using System.Diagnostics;
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
                    window.Content = view;

                    InitializeView();

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

        protected virtual void InitializeView() => window.Show();

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
    }
}
