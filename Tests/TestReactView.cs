using System;
using ReactViewControl;

namespace Tests {

    public class TestReactView : ReactView {

        public event Action<string> Event;

        public class Properties {

            private readonly TestReactView owner;

            public Properties(TestReactView owner) {
                this.owner = owner;
            }

            public void Event(string args) {
                owner.Event(args);
            }
        }

        protected override string JavascriptSource => "/Tests/ReactViewResources/Test/TestApp";

        protected override string NativeObjectName => nameof(TestReactView);

        protected override string ModuleName => "App";

        protected override object CreateNativeObject() {
            return new Properties(this);
        }

        public T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return ExecutionEngine.EvaluateMethod<T>(this, methodCall, args);
        }

        public void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            ExecutionEngine.ExecuteMethod(this, methodCall, args);
        }

        protected override ReactViewFactory Factory => new TestReactViewFactory();
    }
}
