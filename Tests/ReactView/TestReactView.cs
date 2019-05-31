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
                Owner.Loaded();
            }
        }

        public event Action Loaded;

        protected override string JavascriptSource => "/Tests/ReactViewResources/Test/InnerView";

        protected override string NativeObjectName => nameof(InnerViewModule);

        protected override string ModuleName => "InnerView";

        protected override object CreateNativeObject() {
            return new Properties(this);
        }

        protected override string[] Events => new[] { "loaded" };

    }
}
