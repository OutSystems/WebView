namespace ReactViewControl {

    internal class FrameInfo {

        public FrameInfo(string name) {
            Name = name;
        }

        public string Name {get; }

        public IViewModule Component { get; set; }

        public ExecutionEngine ExecutionEngine { get; set; }

        public IViewModule[] Plugins { get; set; }

        public LoadStatus LoadStatus { get; set; }

        public bool PluginsLoaded { get; set; }

        public CustomResourceWithKeyRequestedEventHandler CustomResourceRequested;
    }
}
