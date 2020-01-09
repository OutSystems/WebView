using System;
using System.Windows;
using NUnit.Framework;
using ReactViewControl;

namespace Tests.ReactView {
  
    public class TestReactViewFactoryWithPreload : TestReactViewFactory {
        public override bool EnableViewPreload => true;
    }

    public class TestReactViewWithPreload : TestReactView {

        public TestReactViewWithPreload() {
            AutoShowInnerView = true;
            AttachChildView(new InnerViewModule(), "test");
        }

        protected override ReactViewFactory Factory => new TestReactViewFactoryWithPreload();
    }

    [Ignore("Needs browser setup")]
    public class PreLoadedWebViewTests : ReactViewTestBase<TestReactViewWithPreload> {

        [Test(Description = "Loading a view with a inner view and preload enabled loads the component successfully the second time")]
        public void PreloadLoadsComponent() {
            using (var newView = new TestReactViewWithPreload()) {
                Window.Content = newView;
                WaitFor(() => newView.IsReady, "second view load");
            }
        }

        [Test(Description = "Loading a view with preload enabled uses a webview from cache")]
        public void PreloadUsesWebViewFromCache() {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 1) {
                DoEvents(); // let the cached webview have time to be created
            }
            
            var currentTime = TargetView.EvaluateMethod<double>("getCurrentTime");

            using (var newView = new TestReactViewWithPreload()) {
                Window.Content = newView;
                WaitFor(() => newView.IsReady, "second view load");
                var startTime = newView.EvaluateMethod<double>("getStartTime");
                Assert.LessOrEqual(startTime, currentTime, "The second webview should have been loaded before");
            }
        }
    }
}
