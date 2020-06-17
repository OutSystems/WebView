using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using WebViewControl;

namespace ReactViewControl {

    public abstract class ViewModuleContainer : IViewModule {

        private const string JsEntryFileExtension = ".js.entry";
        private const string CssEntryFileExtension = ".css.entry";

        private IFrame frame;
        private IChildViewHost childViewHost;
        private IDependenciesProvider dependenciesProvider;

        public ViewModuleContainer() {
            DependencyJsSourcesCache = new Lazy<string[]>(() => GetJsDependencies());
            CssSourcesCache = new Lazy<string[]>(() => GetCssDependencies());

            frame = new FrameInfo("dummy");
        }

        private Lazy<string[]> DependencyJsSourcesCache { get; }

        private Lazy<string[]> CssSourcesCache { get; }

        protected virtual string MainJsSource => null;
        protected virtual string NativeObjectName => null;
        protected virtual string ModuleName => null;
        protected virtual string Source => null;

        protected virtual object CreateNativeObject() => null;

        protected virtual string[] Events => new string[0];

        protected virtual string[] DependencyJsSources => new string[0];

        protected virtual string[] CssSources => new string[0];

        protected virtual KeyValuePair<string, object>[] PropertiesValues => new KeyValuePair<string, object>[0];

        string IViewModule.MainJsSource => MainJsSource;

        string IViewModule.NativeObjectName => NativeObjectName;

        string IViewModule.Name => ModuleName;

        string IViewModule.Source => Source;

        object IViewModule.CreateNativeObject() => CreateNativeObject();

        string[] IViewModule.Events => Events;

        string[] IViewModule.DependencyJsSources => DependencyJsSourcesCache.Value;

        string[] IViewModule.CssSources => CssSourcesCache.Value;

        KeyValuePair<string, object>[] IViewModule.PropertiesValues => PropertiesValues;

        void IViewModule.Bind(IFrame frame, IChildViewHost childViewHost, Uri devServerUri) {
            frame.CustomResourceRequestedHandler += this.frame.CustomResourceRequestedHandler;
            frame.ExecutionEngine.MergeWorkload(this.frame.ExecutionEngine);
            this.frame = frame;
            this.childViewHost = childViewHost;

            if (devServerUri != null && dependenciesProvider == null) {
                dependenciesProvider = new HotReloadDependenciesProvider(devServerUri);
            }
        }

        // ease access in generated code
        protected IExecutionEngine ExecutionEngine {
            get {
                var engine = frame.ExecutionEngine;
                if (engine == null) {
                    throw new InvalidOperationException("View module must be bound to an execution engine");
                }
                return engine;
            }
        }

        private string[] GetJsDependencies() {
            var isHotReloadEnabled = dependenciesProvider != null;

            if (isHotReloadEnabled) {
                var dependencies = GetJsDependenciesFromWebPack();
                if (dependencies.Length > 0) {
                    return dependencies;
                }
            }

            return GetDependenciesFromEntriesFile(JsEntryFileExtension);
        }

        private string[] GetCssDependencies() {
            var isHotReloadEnabled = dependenciesProvider != null;

            if (isHotReloadEnabled) {
                var dependencies = GetCssDependenciesFromWebPack();
                if (dependencies.Length > 0) {
                    return dependencies;
                }
            }

            return GetDependenciesFromEntriesFile(CssEntryFileExtension);
        }

        private string[] GetCssDependenciesFromWebPack() {
            return dependenciesProvider.GetCssDependencies(ModuleName);
        }

        private string[] GetJsDependenciesFromWebPack() {
            return dependenciesProvider.GetJsDependencies(ModuleName);
        }

        private string[] GetDependenciesFromEntriesFile(string extension) {
            var entriesFilePath = VirtualPathUtility.GetDirectory(MainJsSource) + Path.GetFileNameWithoutExtension(MainJsSource) + extension;
            var resource = entriesFilePath.Split(new[] { ResourceUrl.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

            using (var stream = GetResourceStream(resource)) {
                if (stream != null) {
                    using (var reader = new StreamReader(stream)) {
                        var allEntries = reader.ReadToEnd();
                        if (allEntries != null && allEntries != string.Empty) {
                            return allEntries.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        }
                    }
                }

            }
            return new string[0];
        }

        private Stream GetResourceStream(string[] resource) {
            return ResourcesManager.TryGetResourceWithFullPath(resource.First(), resource);
        }

        public event CustomResourceRequestedEventHandler CustomResourceRequested {
            add => frame.CustomResourceRequestedHandler += value;
            remove => frame.CustomResourceRequestedHandler -= value;
        }

        public T WithPlugin<T>() {
            return frame.GetPlugin<T>();
        }

        public void Load() {
            childViewHost?.LoadComponent(frame.Name);
        }

        public T GetOrAddChildView<T>(string frameName) where T : IViewModule, new() {
            if (childViewHost == null) {
                return default(T);
            }
            return childViewHost.GetOrAddChildView<T>(frame.Name + (string.IsNullOrEmpty(frame.Name) ? "" : ".") + frameName);
        }

        public ReactView Host => childViewHost.Host;
    }
}