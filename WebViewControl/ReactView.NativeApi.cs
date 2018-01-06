namespace WebViewControl {

    partial class ReactView {

        protected class BaseNativeApi<ConcreteType> {

            protected readonly ConcreteType owner;

            public BaseNativeApi(ConcreteType owner) {
                this.owner = owner;
            }
        }
    }
}