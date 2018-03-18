using System;
using WebViewControl;

namespace Tests {

    public class TestReactView : ReactView {

        public event Action<string> Event;

        public TestReactView() : base(usePreloadedWebView: false) { }

        public class Properties {

            private readonly TestReactView owner;

            public Properties(TestReactView owner) {
                this.owner = owner;
            }

            public void Event(string args) {
                owner.Event(args);
            }
        }

        protected override string JavascriptSource => "/Tests/ReactViewResources/dist/TestApp";

        protected override string JavascriptName => nameof(TestReactView);

        protected override object CreateNativeObject() {
            return new Properties(this);
        }

        public new T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return base.EvaluateMethodOnRoot<T>(methodCall, args);
        }

        public new void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            base.ExecuteMethodOnRoot(methodCall, args);
        }
    }
}
