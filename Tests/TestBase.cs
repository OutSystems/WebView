using System;
using System.Diagnostics;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TestBase {

        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        private Window window;
        private WebView webView;

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
            if (webView == null) {
                webView = new WebView();

                var initialized = false;
                webView.Navigated += (url) => initialized = true;
                webView.LoadHtml("<html><script>;</script><body>Test page</body></html>");

                window.Content = webView;

                // wait for web view to load
                WaitFor(() => initialized, TimeSpan.FromSeconds(10), "webview initialization");
            }

            window.Title = "Running: " + TestContext.CurrentContext.Test.Name;
        }

        [TearDown]
        protected void TearDown() {
            if (!ReuseWebView) {
                webView.Dispose();
                window.Content = null;
                webView = null;
            }
        }

        protected virtual bool ReuseWebView {
            get { return true; }
        }

        protected WebView TargetWebView {
            get { return webView; }
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
        protected void LoadAndWaitReady(string html) {
            var navigated = false;
            TargetWebView.Navigated += (string url) => navigated = true;
            TargetWebView.LoadHtml(html);
            WaitFor(() => navigated, DefaultTimeout);
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
