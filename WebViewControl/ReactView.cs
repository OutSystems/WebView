using System;
using System.Windows.Controls;
using CefSharp.ModelBinding;

namespace WebViewControl {

    public partial class ReactView : ContentControl, IDisposable {

        private static readonly string AssemblyName = typeof(ReactView).Assembly.GetName().Name;
        private static readonly string EmbeddedResourcesPath = AssemblyName + "/Resources/";
        private static readonly string DefaultEmbeddedUrl = EmbeddedResourcesPath + "index.html";
        private static readonly string BuiltinResourcesPath = EmbeddedResourcesPath + "builtin/";

        private readonly WebView webView = new WebView();
        private readonly IBinder defaultBinder = new DefaultBinder(new DefaultFieldNameConverter());

        private long objectCounter;
        private string source;

        public event Action Ready;

        public ReactView() {
            webView.AllowDeveloperTools = true;
            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener("Ready", () => Ready?.Invoke());
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
            set { webView.RegisterJavascriptObject("NativeApi", value, /*InjectTracker*/ bind: ResolveObject); }
        }

        public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return webView.EvaluateScriptFunction<T>(functionName, args);
        }

        public void Dispose() {
            webView.Dispose();
        }
    }
}
