using NUnit.Framework;

namespace Tests {

    public class TestReactCustomInitializationView : TestReactView {

        private readonly string source;
        private readonly object rootProperties;

        public TestReactCustomInitializationView() : base() {
            source = base.Source;
            rootProperties = base.CreateRootPropertiesObject();
            Initialize();
        }

        protected override void Initialize() {
            if (source != null && rootProperties != null) {
                base.Initialize();
            }
        }

        protected override string Source {
            get {
                Assert.NotNull(source);
                return source;
            }
        }

        protected override object CreateRootPropertiesObject() {
            Assert.NotNull(rootProperties);
            return rootProperties;
        }
    }

    public class ReactViewInitializationTests : ReactViewTestBase<TestReactCustomInitializationView> {

        [Test(Description = "Test loading a react component with a fullpath source")]
        public void ComponentWithSourceAndRootPropertiesLateBound() {
            // reaching this point means success
        }
    }
}