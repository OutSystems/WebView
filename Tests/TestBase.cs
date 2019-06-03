using System;
using System.Diagnostics;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NUnit.Framework;

namespace Tests {

    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public abstract class TestBase<T> where T : class, IDisposable, new() {

        protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);

        private Window window;
        private T view;

        protected static string CurrentTestName => TestContext.CurrentContext.Test.Name;

        [OneTimeSetUp]
        protected void OneTimeSetUp() {
            if (Application.Current == null) {
                new Application();
                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            window = new Window();
            window.Show();
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
            Console.Write(" "); // nunit will output the test name if we write on the console
            window.Title = "Running: " + CurrentTestName;
            
            if (view == null) {
                view = CreateView();

                if (view != null) {
                    InitializeView();
                }

                window.Content = view;

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
        }

        protected abstract void ShowDebugConsole();

        protected T TargetView {
            get { return view; }
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
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        private static void DoEvents() {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ => frame.Continue = false), frame);
            Dispatcher.PushFrame(frame);
        }

        protected bool FailOnAsyncExceptions { get; set; } = !Debugger.IsAttached;

        protected void OnUnhandledAsyncException(WebViewControl.UnhandledAsyncExceptionEventArgs e) {
            if (FailOnAsyncExceptions) {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    Assert.Fail("An async exception ocurred: " + e.Exception.ToString());
                }));
            }
        }
    }
}
