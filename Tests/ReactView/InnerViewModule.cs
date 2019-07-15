using System;
using ReactViewControl;

namespace Tests.ReactView {

    public class InnerViewModule : ViewModuleContainer {

        public class Properties {

            private InnerViewModule Owner { get; }

            public Properties(InnerViewModule owner) {
                Owner = owner;
            }

            public void Loaded() {
                Owner.Loaded?.Invoke();
            }

            public void MethodCalled() {
                Owner.MethodCalled?.Invoke();
            }
        }

        public event Action Loaded;

        public event Action MethodCalled;

        public void TestMethod() {
            ExecutionEngine.ExecuteMethod(this, "testMethod");
        }

        protected override string MainSource => "/Tests/ReactViewResources/Test/InnerView";

        protected override string NativeObjectName => nameof(InnerViewModule);

        protected override string ModuleName => "InnerView";

        protected override object CreateNativeObject() {
            return new Properties(this);
        }

        protected override string[] Events => new[] { "loaded", "methodCalled" };

    }
}
