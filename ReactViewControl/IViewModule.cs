using System.Collections.Generic;

namespace ReactViewControl {

    public interface IViewModule {

        string MainSource { get; }

        string OriginalSourceFolder { get; }

        string NativeObjectName { get; }

        string Name { get; }

        string Source { get; }

        string[] ExternalSources { get; }

        object CreateNativeObject();

        string[] Events { get; }

        KeyValuePair<string, object>[] PropertiesValues { get; }

        void Bind(IExecutionEngine engine);

        IExecutionEngine Engine { get; }
    }
}
