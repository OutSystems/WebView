using System;
using System.Collections.Generic;
using ReactViewControl;

namespace Tests.ReactView {

    public class TestReactViewModule : ViewModuleContainer {

        public class Properties {

            private TestReactViewModule Owner { get; }

            public Properties(TestReactViewModule owner) {
                Owner = owner;
            }

            public void Event(string args) {
                Owner.Event?.Invoke(args);
            }
        }

        public event Action<string> Event;

        public T EvaluateMethod<T>(string functionName, params object[] args) {
            return ExecutionEngine.EvaluateMethod<T>(this, functionName, args);
        }

        public void ExecuteMethod(string functionName, params object[] args) {
            ExecutionEngine.ExecuteMethod(this, functionName, args);
        }

        public string PropertyValue { get; set; }

        public bool AutoShowInnerView { get; set; }

        public InnerViewModule InnerView { get => GetOrAddChildView<InnerViewModule>("test"); }

        protected override string MainJsSource => "/Tests.ReactView/Generated/TestApp.js";

        protected override string NativeObjectName => nameof(TestReactView);

        protected override string ModuleName => "TestApp";

        protected override object CreateNativeObject() {
            return new Properties(this);
        }

        protected override string[] Events => new[] { "event" };

        protected override KeyValuePair<string, object>[] PropertiesValues => new[] {
            new KeyValuePair<string, object>("propertyValue", PropertyValue),
            new KeyValuePair<string, object>("autoShowInnerView", AutoShowInnerView)
        };

    }

    public class TestReactView : ReactViewControl.ReactView {

        public TestReactView() : this(new TestReactViewModule()) {
            EnableDebugMode = true;
        }

        protected TestReactView(TestReactViewModule module) : base(module) { }

        protected override ReactViewFactory Factory => new TestReactViewFactory();

        protected new TestReactViewModule MainModule { get => (TestReactViewModule) base.MainModule; }

        public event Action<string> Event { add => MainModule.Event += value; remove => MainModule.Event -= value; }

        public string PropertyValue { get => MainModule.PropertyValue; set => MainModule.PropertyValue = value; }

        public bool AutoShowInnerView { get => MainModule.AutoShowInnerView; set => MainModule.AutoShowInnerView = value; }

        public InnerViewModule InnerView => MainModule.InnerView;

        public T EvaluateMethod<T>(string functionName, params object[] args) {
            return MainModule.EvaluateMethod<T>(functionName, args);
        }

        public void ExecuteMethod(string functionName, params object[] args) {
            MainModule.ExecuteMethod(functionName, args);
        }
    }
}
