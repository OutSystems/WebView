using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TestBase {

        private Window window;
        private WebView webView;

        [OneTimeSetUp]
        protected void OneTimeSetUp() {
            if (Application.Current == null) {
                new Application();
                Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            }

            window = new Window();
            window.Show();
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
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => Thread.Sleep(1)));
            }

            if (!predicate()) {
                throw new TimeoutException("Timed out waiting for " + purpose);
            }
        }
    }
}
