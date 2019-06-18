using System;
using ReactViewControl;

namespace Example {

    public abstract class ExtendedReactView : ReactView {

        protected override ReactViewFactory Factory => new ExtendedReactViewFactory();

        public ExtendedReactView(IViewModule mainModule) : base(mainModule) {
#if DEBUG
            EnableHotReload = true;
#endif
            WithPlugin<Plugin>().NotifyPluginLoaded += OnNotifyPluginLoaded;
        }

        private void OnNotifyPluginLoaded() {
            Console.WriteLine("On plugin loaded");
        }
    }
}
