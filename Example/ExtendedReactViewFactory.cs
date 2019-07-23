using ReactViewControl;
using WebViewControl;

namespace Example {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(ReactViewExample).Assembly, "ExampleView", "DefaultStyleSheet.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
                new ViewPlugin()
            };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => true;
    }
}
