using System;

namespace WebViewControl {
    public interface IReactView : IDisposable {
        string AdditionalModule { get; set; }
        string DefaultStyleSheet { get; set; }
        bool EnableDebugMode { get; set; }
        bool IsReady { get; }

        event Action Ready;
        event Action<UnhandledExceptionEventArgs> UnhandledAsyncException;

        void Dispose();
    }
}