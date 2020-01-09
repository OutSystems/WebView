namespace ReactViewControl {

    public interface IFrame {

        string Name { get; }

        IExecutionEngine ExecutionEngine { get; }

        IChildViewHost ChildViewHost { get; }

        T GetPlugin<T>();

        CustomResourceRequestedEventHandler CustomResourceRequestedHandler { get; set; }
    }
}
