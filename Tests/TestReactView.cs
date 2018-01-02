using System;
using WebViewControl;

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

        protected override object CreateRootPropertiesObject() {
            return new Properties(this);
        }

        protected override string Source => "ReactViewResources/dist/TestApp";

        public new void ExecuteScript(string script) {
            base.ExecuteScript(script);
        }

        public new T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return base.EvaluateMethodOnRoot<T>(methodCall, args);
        }

        public new void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            base.ExecuteMethodOnRoot(methodCall, args);
        }
    }
}
