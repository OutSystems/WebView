using System;
using ReactViewControl;
using WebViewControl;

namespace Example {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(ReactViewExample).Assembly, "Generated", "DefaultStyleSheet.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
                new ViewPlugin()
            };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => true;

#if DEBUG
        public override Uri DevServerURI => new Uri("http://localhost:8080");
#endif
    }
}
