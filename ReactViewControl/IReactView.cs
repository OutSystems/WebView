using System;
using WebViewControl;

namespace ReactViewControl {
    public interface IReactView : IDisposable {
        IViewModule[] Plugins { get; set; }
        ResourceUrl DefaultStyleSheet { get; set; }
        bool EnableDebugMode { get; set; }
        bool IsReady { get; }
        double ZoomPercentage { get; set; }

        event Action Ready;
        event Action<UnhandledAsyncExceptionEventArgs> UnhandledAsyncException;

        T WithPlugin<T>();
    }
}