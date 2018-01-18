using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace WebViewControl {

    public partial class ReactView : ContentControl, IDisposable {

        private const string RootObject = "__Root__";
        private const string ReadyEventName = "Ready";

        private static readonly string AssemblyName = typeof(ReactView).Assembly.GetName().Name;
        private static readonly string EmbeddedResourcesPath = AssemblyName + "/Resources/";
        private static readonly string DefaultEmbeddedUrl = EmbeddedResourcesPath + "index.html";

        private readonly WebView webView = new WebView();
        private readonly Assembly userCallingAssembly;

        private bool isSourceSet = false;
        private Listener readyEventListener;

        public ReactView() {
            userCallingAssembly = WebView.GetUserCallingAssembly();

            webView.AllowDeveloperTools = true;
            webView.DisableBuiltinContextMenus = true;
            webView.IgnoreMissingResources = false;
            webView.AttachListener(ReadyEventName, () => IsReady = true, executeInUI: false);

            var rootPropertiesObject = CreateRootPropertiesObject();
            if (rootPropertiesObject != null) {
                webView.RegisterJavascriptObject("__RootProperties__", rootPropertiesObject, executeCallsInUI:true);
            }
            
            Content = webView;
        }

        public event Action Ready {
            add { readyEventListener = webView.AttachListener(ReadyEventName, value); }
            remove {
                if (readyEventListener != null) {
                    webView.DetachListener(readyEventListener);
                }
            }
        }

        public event Action<Exception> UnhandledAsyncException {
            add { webView.UnhandledAsyncException += value; }
            remove { webView.UnhandledAsyncException -= value; }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            if (!isSourceSet) {
                // wait for default stylesheet property to be set
                LoadSource();
                isSourceSet = true;
            }
        }

        private void LoadSource() {
            var source = Source ?? "";
            if (!source.EndsWith(".js")) {
                source += ".js";
            }

            var callingAssemblyName = userCallingAssembly.GetName().Name;

            var urlParams = new List<string>() {
                "/" + EmbeddedResourcesPath,
                source.StartsWith("/") ? source : $"/{callingAssemblyName}/{source}"
            };

            if (DefaultStyleSheet != null) { 
                urlParams.Add(DefaultStyleSheet.StartsWith("/") ? DefaultStyleSheet : $"/{callingAssemblyName}/{DefaultStyleSheet}");
            }

            var url = WebView.BuildEmbeddedResourceUrl(AssemblyName, DefaultEmbeddedUrl + "?" + string.Join("&", urlParams));
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

        protected virtual string Source => null;

        public static readonly DependencyProperty DefaultStyleSheetProperty = DependencyProperty.Register(
            "DefaultStyleSheet", 
            typeof(string),
            typeof(ReactView));

        public string DefaultStyleSheet {
            get { return (string) GetValue(DefaultStyleSheetProperty); }
            set { SetValue(DefaultStyleSheetProperty, value); }
        }

        public bool IsReady { get; private set; }
    }
}
