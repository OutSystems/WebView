namespace WebViewControl {

    public interface IExecutionEngine {
        T EvaluateMethod<T>(IViewModule module, string functionName, params object[] args);
        void ExecuteMethod(IViewModule module, string functionName, params object[] args);
    }
}
