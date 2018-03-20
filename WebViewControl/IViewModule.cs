namespace WebViewControl {

    public interface IViewModule {

        string JavascriptSource { get; }

        string JavascriptName { get; }

        string Source { get; }

        object CreateNativeObject();
    }
}
