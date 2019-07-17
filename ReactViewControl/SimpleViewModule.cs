using WebViewControl;

namespace ReactViewControl {

    public class SimpleViewModule : ViewModuleContainer {

        private readonly string source;
        private readonly string moduleName;

        public SimpleViewModule(string moduleName, ResourceUrl url) {
            this.moduleName = moduleName;
            source = url.ToString();
        }

        protected override string MainJsSource => source;

        protected override string ModuleName => moduleName;
    }
}
