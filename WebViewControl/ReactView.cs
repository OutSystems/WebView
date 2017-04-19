using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;

namespace WebViewControl {

    public class ReactView : ContentControl {

        public struct TrackCode {
            public long Value;
        }

        public class ViewObject {
            public TrackCode TrackCode;
        }

        private readonly Dictionary<long, object> jsObjects = new Dictionary<long, object>();
        private readonly InternalReactWebView reactWebView;

        private long objectCounter;
        private string source;

        public ReactView() {
            reactWebView = new InternalReactWebView(this, new JsObjectBinder(this), new JsObjectInterceptor(this));
            reactWebView.AllowDeveloperTools = true;
            reactWebView.DisableBuiltinContextMenus = true;
            Content = reactWebView;
        }

        public string Source {
            get { return source; }
            set {
                source = value;
                var currentAssembly = this.GetType().Assembly;
                var callingAssemblies = new StackTrace().GetFrames().Select(f => f.GetMethod().ReflectedType.Assembly).SkipWhile(a => a == currentAssembly);
                var userAssembly = callingAssemblies.First(a => !IsFrameworkAssemblyName(a.GetName().Name));
                if (userAssembly == null) {
                    throw new InvalidOperationException("Unable to find calling assembly");
                }
                reactWebView.Load(userAssembly, (source ?? "").Split('/'));
            }
        }

        public object NativeApi {
            set { reactWebView.RegisterJavascriptObject("NativeApi", value); }
        }

        internal object GetTrackedObject(long id) {
            object obj;
            if (jsObjects.TryGetValue(id, out obj)) {
                return obj;
            }
            throw new InvalidOperationException("Unknown object with track code:");
        }

        internal void TrackObject(object obj) {
            var viewObj = obj as ViewObject;
            if (viewObj == null) {
                throw new InvalidOperationException("Object is not a view object");
            }

            if (viewObj.TrackCode.Value == 0) {
                viewObj.TrackCode.Value = ++objectCounter;
                jsObjects[viewObj.TrackCode.Value] = obj;
            }
        }

        private static bool IsFrameworkAssemblyName(string name) {
            return name == "PresentationFramework" || name == "mscorlib" || name == "System.Xaml";
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return reactWebView.EvaluateScriptFunction<T>(functionName, args);
        }
    }
}
