using System.Threading.Tasks;

namespace Example {

    partial class ExampleViewModule {
    }

    partial class ExampleViewAdapter {

        public ExampleViewAdapter(IExampleViewModule component) {
            Component = component;
        }

        private IExampleViewModule Component { get; }
    }
}
