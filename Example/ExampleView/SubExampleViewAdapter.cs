namespace Example {

    partial class SubExampleViewAdapter {

        public SubExampleViewAdapter(ISubExampleView component) {
            Component = component;
        }

        private ISubExampleView Component { get; }
    }
}
