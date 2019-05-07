using ReactViewControl;
using System;
using WebViewControl;

namespace Example {

    public class ExtendedReactView : ReactView {

        private class ViewFactory : ReactViewFactory {

            public override ResourceUrl DefaultStyleSheet => new ResourceUrl(typeof(ReactViewExample).Assembly, "ExampleView", "DefaultStyleSheet.css");

            public override IViewModule[] Plugins {
                get {
                    return new[]{
                        new Plugin()
                    };
                }
            }

            public override bool ShowDeveloperTools => true;
        }

        protected override ReactViewFactory Factory => new ViewFactory();

        public ExtendedReactView() {
            WithPlugin<Plugin>().NotifyPluginLoaded += OnNotifyPluginLoaded;
        }

        private void OnNotifyPluginLoaded() {
            Console.WriteLine("On plugin loaded");
        }
    }
}
