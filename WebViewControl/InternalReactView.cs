using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class InternalReactWebView : WebView {

        private const string ReactRootComponentPath = "webview-root-component.js";

        private static readonly Assembly BuiltinResourcesAssembly = Assembly.GetExecutingAssembly();

        private readonly ReactView reactView;
        private readonly IBinder binder;
        private readonly IInterceptor interceptor;

        private string[] componentSource;

        public InternalReactWebView(ReactView reactView, IBinder binder, IInterceptor interceptor) : base() {
            this.reactView = reactView;
            this.binder = binder;
            this.interceptor = interceptor;
        }

        private static bool IsBuiltinResource(Uri uri) {
            return uri.Segments.Length > 1 && uri.Segments[1] == BuiltinResourcesPath;
        }

        public void Load(Assembly assembly, params string[] source) {
            componentSource = source;
            LoadFrom(assembly, componentSource.First());
        }

        protected override Assembly ResolveResourceAssembly(Uri resourceUrl) {
            if (resourceUrl.AbsoluteUri == DefaultEmbeddedUrl ||
                (IsBuiltinResource(resourceUrl) && resourceUrl.Segments.LastOrDefault() != ReactRootComponentPath)) {
                // builtin resources are loaded from current assembly except root component (loaded from calling assembly)
                return BuiltinResourcesAssembly;
            }
            return base.ResolveResourceAssembly(resourceUrl);
        }

        protected override string[] ResolveResourcePath(Uri resourceUrl) {
            if (resourceUrl.AbsoluteUri == DefaultEmbeddedUrl || IsBuiltinResource(resourceUrl)) {
                if (resourceUrl.Segments.LastOrDefault() == ReactRootComponentPath) {
                    // root component is the component specified for this control
                    return componentSource;
                }
                return base.ResolveResourcePath(resourceUrl);
            }
            return componentSource.Take(componentSource.Length - 1).Concat(resourceUrl.Segments.Skip(1)).ToArray();
        }

        public override void RegisterJavascriptObject(string name, object objectToBind) {
            var options = new BindingOptions() {
                Binder = binder,
                Interceptor = interceptor,
            };
            chromium.RegisterAsyncJsObject(name, objectToBind, options);
        }
    }
}
