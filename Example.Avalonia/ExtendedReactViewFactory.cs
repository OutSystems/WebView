using ReactViewControl;
using WebViewControl;

namespace Example.Avalonia {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        //public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", "DefaultStyleSheet.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
                new ViewPlugin()
            };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => true;

        public override bool SyncNativeCalls => true;
    }
}
