using NUnit.Framework;

namespace Tests {

    public class CommonTests : WebViewTestBase {

        [Test(Description = "Attached listeners are called")]
        public void Listeners() {
            var listenerCalled = false;
            var listener = TargetView.AttachListener("event_name", () => listenerCalled = true);
            LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");
            WaitFor(() => listenerCalled, DefaultTimeout);
            Assert.IsTrue(listenerCalled);
        }

        [Test(Description = "Before navigate hook is called")]
        public void BeforeNavigateHookCalled() {
            var beforeNavigatedCalled = false;
            TargetView.BeforeNavigate += (request) => {
                request.Cancel();
                beforeNavigatedCalled = true;
            };
            TargetView.Address = "https://www.google.com";
            WaitFor(() => beforeNavigatedCalled, DefaultTimeout);
            Assert.IsTrue(beforeNavigatedCalled);
        }
    }
}
