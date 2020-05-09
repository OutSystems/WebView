﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WebViewControl;

namespace ReactViewControl {

    public abstract class ViewModuleContainer : IViewModule {

        private const string JsEntryFileExtension = ".js.entry";
        private const string CssEntryFileExtension = ".css.entry";

        private IFrame frame;
        private IChildViewHost childViewHost;

        public ViewModuleContainer() {
            DependencyJsSourcesCache = new Lazy<string[]>(() => GetDependenciesFromEntriesFile(JsEntryFileExtension));
            CssSourcesCache = new Lazy<string[]>(() => GetDependenciesFromEntriesFile(CssEntryFileExtension));

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

        void IViewModule.Bind(IFrame frame, IChildViewHost childViewHost) {
            frame.CustomResourceRequestedHandler += this.frame.CustomResourceRequestedHandler;
            frame.ExecutionEngine.MergeWorkload(this.frame.ExecutionEngine);
            this.frame = frame;
            this.childViewHost = childViewHost;
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

        private string[] GetDependenciesFromEntriesFile(string extension) {
            var entriesFilePath = Path.Combine(Path.GetDirectoryName(MainJsSource), Path.GetFileNameWithoutExtension(MainJsSource) + extension);
            var resource = entriesFilePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

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
            var isHotReloadEnabled = childViewHost == null ? false : childViewHost.IsHotReloadEnabled;
            if (isHotReloadEnabled) {
                return File.OpenRead(Path.Combine(Path.GetDirectoryName(Source), resource.Last()));
            }

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
            childViewHost?.LoadComponent(frame.Name, this);
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
