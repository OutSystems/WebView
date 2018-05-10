namespace WebViewControl {

    public interface IExecutionEngine {
        T EvaluateMethod<T>(IViewModule module, string functionName, params string[] args);
        void ExecuteMethod(IViewModule module, string functionName, params string[] args);
    }
}
