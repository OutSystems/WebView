using System;

namespace ReactViewControl {

    public interface IChildViewHost {
        void LoadComponent(string frameName);

        T GetOrAddChildView<T>(string frameName) where T : IViewModule, new();

        ReactView Host { get; }

        bool IsHotReloadEnabled { get; }

        Uri DevServerUri { get; }
    }
}
