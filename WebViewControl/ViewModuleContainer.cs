namespace WebViewControl {

    public abstract class ViewModuleContainer : IViewModule {

        protected virtual string JavascriptSource => null;
        protected virtual string JavascriptName => null;

        protected virtual object CreateNativeObject() {
            return null;
        }

        string IViewModule.JavascriptSource => JavascriptSource;

        string IViewModule.JavascriptName => JavascriptName;

        object IViewModule.CreateNativeObject() {
            return CreateNativeObject();
        }
    }
}
