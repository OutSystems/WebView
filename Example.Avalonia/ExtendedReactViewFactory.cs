using System;
using ReactViewControl;
using WebViewControl;

namespace Example.Avalonia {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        public override ResourceUrl DefaultStyleSheet => Settings.IsBorderLessPreference ?
            new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", "DefaultStyleSheet.css") :
            new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", "DefaultStyleSheetWithBorders.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
                new ViewPlugin()
            };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => true;

        public override int MaxNativeMethodsParallelCalls => 1;

#if DEBUG
        public override Uri DevServerURI => null;
#endif
    }
}
