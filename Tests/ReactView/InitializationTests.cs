using System.Windows;
using NUnit.Framework;

namespace Tests.ReactView {

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

    public class InitializationTests : ReactViewTestBase<TestReactCustomInitializationView> {

        [Test(Description = "Test loading a react component with a fullpath source")]
        public void ComponentWithSourceAndRootPropertiesLateBound() {
            // reaching this point means success
        }
    }
}