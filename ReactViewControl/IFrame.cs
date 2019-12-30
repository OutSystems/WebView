namespace ReactViewControl {

    public interface IFrame {

        string Name { get; }

        IExecutionEngine ExecutionEngine { get; }

        T GetPlugin<T>();

        CustomResourceRequestedEventHandler CustomResourceRequestedHandler { get; set; }
    }
}
