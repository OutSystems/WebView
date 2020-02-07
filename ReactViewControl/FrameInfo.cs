using System;
using System.Linq;

namespace ReactViewControl {

    internal class FrameInfo : IFrame {

        public FrameInfo(string name) {
            Name = name;
            Plugins = new IViewModule[0];
            ExecutionEngine = new ExecutionEngine();
        }

        public string Name {get; }

        public IViewModule Component { get; set; }

        public ExecutionEngine ExecutionEngine { get; private set; }

        IExecutionEngine IFrame.ExecutionEngine => ExecutionEngine;

        public IViewModule[] Plugins { get; set; }

        public LoadStatus LoadStatus { get; set; }

        public bool IsComponentReadyToLoad { get; set; }

        public CustomResourceRequestedEventHandler CustomResourceRequestedHandler { get; set; }

        public T GetPlugin<T>() {
            var plugin = Plugins.OfType<T>().FirstOrDefault();
            if (plugin == null) {
                throw new InvalidOperationException($"Plugin {typeof(T).Name} not found in {Name}");
            }
            return plugin;
        }

        public void Reset() {
            ExecutionEngine = new ExecutionEngine();
            LoadStatus = LoadStatus.Initialized;
            IsComponentReadyToLoad = false;
        }
    }
}
