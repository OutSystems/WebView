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
            webView = new WebView();

            var initialized = false;
            webView.Navigated += (url) => initialized = true;
            webView.LoadHtml("<html><script>;</script><body>Test page</body></html>");

            window.Content = webView;
            window.Show();

            // wait for web view to load
            var start = DateTime.Now;
            while (!initialized && (DateTime.Now - start).TotalSeconds < 10 && Application.Current != null) {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => Thread.Sleep(1)));
            }

            if (!initialized) {
                throw new TimeoutException("Timed out waiting for webview to initialize");
            }
        }

        [SetUp]
        protected void SetUp() {
            window.Title = "Running: " + TestContext.CurrentContext.Test.Name;
        }

        protected WebView TargetWebView {
            get { return webView; }
        }
    }
}
