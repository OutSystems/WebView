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

        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        private Window window;
        private T view;

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
            window.Close();
        }

        [SetUp]
        protected void SetUp() {
            window.Title = "Running: " + TestContext.CurrentContext.Test.Name;
            if (view == null) {
                view = new T();

                window.Content = view;

                InitializeView();
            }
        }

        protected abstract void InitializeView();

        [TearDown]
        protected void TearDown() {
            if (!ReuseView) {
                view.Dispose();
                window.Content = null;
                view = null;
            }
        }

        protected virtual bool ReuseView {
            get { return true; }
        }

        protected T TargetView {
            get { return view; }
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
    }
}
