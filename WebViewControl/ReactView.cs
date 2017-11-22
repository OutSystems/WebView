using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Controls;
using CefSharp.ModelBinding;

namespace WebViewControl {

    public class ReactView : ContentControl, IDisposable {

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
        private readonly WebView webView = new WebView();
        private readonly IBinder defaultBinder = new DefaultBinder(new DefaultFieldNameConverter());

        private long objectCounter;
        private string source;

        public event Action Ready;

        public ReactView() {
            webView.AllowDeveloperTools = true;
            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            Content = webView;
            webView.AttachListener("Ready", () => Ready?.Invoke());
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
            set { webView.RegisterJavascriptObject("NativeApi", value, InjectTracker, Bind); }
        }

        internal object GetTrackedObject(long id) {
            object obj;
            if (jsObjects.TryGetValue(id, out obj)) {
                return obj;
            }
            throw new InvalidOperationException("Unknown object with track code:");
        }
        
        private object Bind(object obj, Type modelType) {
            if (modelType.IsInterface) {
                // TODO check if its a ViewObject
                // int .. might not be enough
                // trackcode
                var trackCode = (int)((Dictionary<string, object>)obj)["Value"];
                return GetTrackedObject(trackCode);
            }
            return defaultBinder.Bind(obj, modelType);
        }

        private object InjectTracker(Func<object> originalMethod) {
            object result = originalMethod();
            if (result != null) {
                if (result is IEnumerable) {
                    var objects = (IEnumerable)result;
                    foreach (object item in objects) {
                        TrackObject(item);
                    }
                } else {
                    TrackObject(result);
                }
            }
            return result;
        }

        private void TrackObject(object obj) {
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

        public void Dispose() {
            webView.Dispose();
        }
    }
}
