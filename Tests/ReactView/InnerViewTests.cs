using NUnit.Framework;

namespace Tests.ReactView {

    public class InnerViewTests : ReactViewTestBase {

        protected override void AfterInitializeView() {
            TargetView.AutoShowInnerView = true;
            base.AfterInitializeView();
        }

        [Test(Description = "Tests inner view load")]
        public void InnerViewIsLoaded() {
            var innerViewLoaded = false;
            var innerView = new InnerViewModule();
            innerView.Loaded += () => innerViewLoaded = true;

            TargetView.AttachInnerView(innerView, "test");

            WaitFor(() => innerViewLoaded, "inner view module load");

            Assert.IsTrue(innerViewLoaded);
        }
    }
}
