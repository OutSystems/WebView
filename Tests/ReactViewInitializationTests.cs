using NUnit.Framework;

namespace Tests {

    public class TestReactCustomInitializationView : TestReactView {

        private readonly object component;

        public TestReactCustomInitializationView() : base() {
            component = base.CreateNativeObject();
        }

        protected override object CreateNativeObject() {
            Assert.IsNotNull(component);
            return base.CreateNativeObject();
        }
    }

    public class ReactViewInitializationTests : ReactViewTestBase<TestReactCustomInitializationView> {

        private const string PropertyValue = "test value";

        protected override void AfterInitializeView() {
            base.AfterInitializeView();
            TargetView.PropertyValue = PropertyValue;
        }

        [Test(Description = "Test loading a react component with a fullpath source")]
        public void ComponentWithSourceAndRootPropertiesLateBound() {
            // reaching this point means success
        }


        [Test(Description = "Test setting properties after component initialization.")]
        public void PropertyValuesArePassedToView() {
            WaitFor(() => TargetView.IsReady, "View is ready");
            var actualPropertyValue = TargetView.EvaluateMethodOnRoot<string>("getPropertyValue");
            Assert.AreEqual(PropertyValue, actualPropertyValue);
        }
    }
}