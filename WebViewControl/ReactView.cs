using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WebViewControl {

    public abstract partial class ReactView : ContentControl, IDisposable {

        private const string RootObject = "__Root__";

        private static readonly string AssemblyName = typeof(ReactView).Assembly.GetName().Name;
        private static readonly string EmbeddedResourcesPath = AssemblyName + "/Resources/";
        private static readonly string DefaultEmbeddedUrl = EmbeddedResourcesPath + "index.html";

        private readonly WebView webView = new WebView();

        public event Action Ready;

        public ReactView() {
            webView.AllowDeveloperTools = true;
            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener("Ready", () => Ready?.Invoke());

            SetSource(Source);

            var rootPropertiesObject = CreateRootPropertiesObject();
            if (rootPropertiesObject != null) {
                webView.RegisterJavascriptObject("__RootProperties__", rootPropertiesObject, ExecuteNativeCallsOnUI);
            }

            Content = webView;
        }

        protected void SetSource(string value) {
            value = value ?? "";
            if (!value.EndsWith(".js")) {
                value += ".js";
            }
            var userCallingAssembly = WebView.GetUserCallingAssembly();
            var url = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultEmbeddedUrl + "?" + "/" + EmbeddedResourcesPath + "&/" + userCallingAssembly.GetName().Name + "/" + value);
            webView.Address = url;
        }

        public void Dispose() {
            webView.Dispose();
        }

        protected void ExecuteMethodOnRoot(string methodCall, params string[] args) {
            webView.ExecuteScriptFunction(RootObject + "." + methodCall, args);
        }

        protected T EvaluateMethodOnRoot<T>(string methodCall, params string[] args) {
            return webView.EvaluateScriptFunction<T>(RootObject + "." + methodCall, args);
        }

        protected virtual object CreateRootPropertiesObject() {
            return null;
        }

        protected abstract string Source { get; }

        private object ExecuteNativeCallsOnUI(Func<object> originalFunc, CancellationToken cancellationToken) {
            var op = Dispatcher.InvokeAsync(originalFunc, DispatcherPriority.Normal, cancellationToken);
            op.Task.Wait(cancellationToken);
            return op.Result;
        }
    }
}
