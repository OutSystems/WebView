namespace WebViewControl {

    partial class ReactView {

        public class Properties<ConcreteType> {

            protected readonly ConcreteType owner;

            public Properties(ConcreteType owner) {
                this.owner = owner;
            }
        }
    }
}