using System;

namespace WebViewControl {

    partial class ReactView {

        public class BaseNativeApi<ConcreteType> {

            protected readonly ConcreteType owner;

            public BaseNativeApi(ConcreteType owner) {
                this.owner = owner;
            }

            public void Ready() {
            }

            public event Action Ready;
        }
    }
}