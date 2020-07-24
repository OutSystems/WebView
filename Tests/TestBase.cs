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

        protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);

        private Window window;
        private T view;

        protected static string CurrentTestName => TestContext.CurrentContext.Test.Name;

        [OneTimeSetUp]
        protected void OneTimeSetUp() {
            if (Application.Current == null) {
               AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            }
        }

        [OneTimeTearDown]
        protected void OneTimeTearDown() {
            if (view != null) {
                view.Dispose();
            }
            window.Close();
        }

        [SetUp]
        protected void SetUp() {
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
                    AfterInitializeView();
                }
            }
        }

        protected Window Window => window;

        protected virtual T CreateView() {
            return new T();
        }

        protected virtual void InitializeView() { }

        protected virtual void AfterInitializeView() { }

        [TearDown]
        protected void TearDown() {
            if (Debugger.IsAttached && TestContext.CurrentContext.Result.FailCount > 0) {
                ShowDebugConsole();
                WaitFor(() => false, TimeSpan.MaxValue);
                return;
            }
            if (view != null) {
                view.Dispose();
                view = null;
            }
            window.Content = null;
            window.Close();
        }

        protected abstract void ShowDebugConsole();

        protected T TargetView {
            get { return view; }
        }

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
