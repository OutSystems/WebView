namespace WebViewControl {

    public interface IViewModule {

        string JavascriptSource { get; }

        string NativeObjectName { get; }

        string Name { get; }

        string Source { get; }

        object CreateNativeObject();

        void Bind(IExecutionEngine engine);

        IExecutionEngine ExecutionEngine { get; }
    }
}
