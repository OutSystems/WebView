namespace WebViewControl {

    public abstract class ViewModuleContainer : IViewModule {

        private IExecutionEngine engine;

        protected virtual string JavascriptSource => null;
        protected virtual string NativeObjectName => null;
        protected virtual string ModuleName => null;
        protected virtual string Source => null;

        protected virtual object CreateNativeObject() {
            return null;
        }

        string IViewModule.JavascriptSource => JavascriptSource;

        string IViewModule.NativeObjectName => NativeObjectName;

        string IViewModule.Name => ModuleName;

        string IViewModule.Source => Source;

        object IViewModule.CreateNativeObject() {
            return CreateNativeObject();
        }

        void IViewModule.Bind(IExecutionEngine engine) {
            this.engine = engine;
        }

        protected IExecutionEngine ExecutionEngine => engine; // ease access in generated code

        IExecutionEngine IViewModule.ExecutionEngine => ExecutionEngine;
    }
}
