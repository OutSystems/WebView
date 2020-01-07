using System.Collections.Generic;

namespace ReactViewControl {

    public interface IViewModule {

        string MainJsSource { get; }

        string[] DependencyJsSources { get; }

        string[] CssSources { get; }

        string NativeObjectName { get; }

        string Name { get; }

        string Source { get; }

        object CreateNativeObject();

        string[] Events { get; }

        KeyValuePair<string, object>[] PropertiesValues { get; }

        void Bind(IFrame frame);

        void AttachTo(IChildViewHost host, string frameName);

        T WithPlugin<T>();

        event CustomResourceRequestedEventHandler CustomResourceRequested;
    }
}
