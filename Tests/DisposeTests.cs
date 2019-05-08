using NUnit.Framework;
using System.Windows;
using WebViewControl;

namespace Tests {

    public class DisposeTests : TestBase<WebView> {

        protected override void InitializeView() { }

        [Test(Description = "Tests that the webview is disposed when host window is not shown")]
        public void WebViewIsDisposedWhenHostWindowIsNotShown() {
            var view = new WebView();
            var window = new Window();
            window.Content = view;

            var disposed = false;
            view.Disposed += () => disposed = true;

            window.Close();
            Assert.IsTrue(disposed);
        }
    }
}