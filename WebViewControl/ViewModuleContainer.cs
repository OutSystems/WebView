namespace WebViewControl {

    public abstract class ViewModuleContainer : IViewModule {

        protected virtual string JavascriptSource => null;
        protected virtual string JavascriptName => null;
        protected virtual string Source => null;

        protected virtual object CreateNativeObject() {
            return null;
        }

        string IViewModule.JavascriptSource => JavascriptSource;

        string IViewModule.JavascriptName => JavascriptName;

        string IViewModule.Source => Source;

        object IViewModule.CreateNativeObject() {
            return CreateNativeObject();
        }
    }
}
