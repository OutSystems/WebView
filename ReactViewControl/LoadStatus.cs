namespace ReactViewControl {

    internal enum LoadStatus {
        Initialized,
        ViewInitialized, // page/frame is initialized but component not loaded yet
        ComponentLoading, // component is loading
        Ready // component is ready
    }
}
