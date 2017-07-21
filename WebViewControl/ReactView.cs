using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace WebViewControl {

    public class ReactView : ContentControl {

        private static readonly string AssemblyName = typeof(ReactView).Assembly.GetName().Name;
        private static readonly string EmbeddedResourcesPath = AssemblyName + "/Resources/";
        private static readonly string DefaultEmbeddedUrl = EmbeddedResourcesPath + "index.html";
        private static readonly string BuiltinResourcesPath = EmbeddedResourcesPath + "builtin/";

        public struct TrackCode {
            public long Value;
        }

        public class ViewObject {
            public TrackCode TrackCode;
        }

        private readonly Dictionary<long, object> jsObjects = new Dictionary<long, object>();
        private readonly WebView webView;

        private long objectCounter;
        private string source;

        public ReactView() {
            webView = new WebView();
            webView.AllowDeveloperTools = true;
            webView.DisableBuiltinContextMenus = true;
            Content = webView;
        }

        public string Source {
            get { return source; }
            set {
                source = value ?? "";
                if (!source.EndsWith(".js")) {
                    source += ".js";
                }
                var userCallingAssembly = WebView.GetUserCallingAssembly();
                var url = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultEmbeddedUrl + "?" + "/" + BuiltinResourcesPath + "&/" + userCallingAssembly.GetName().Name + "/" + Source);
                webView.Address = url;
            }
        }

        public object NativeApi {
            set { webView.RegisterJavascriptObject("NativeApi", value); }
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

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return webView.EvaluateScriptFunction<T>(functionName, args);
        }
    }
}
