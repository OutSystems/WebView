using System;
using System.Linq;
using System.Reflection;
using CefSharp;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class InternalReactWebView : WebView {

        private static readonly string EmbeddedResourcesPath = EmbeddedScheme + DefaultPath + typeof(InternalReactWebView).Assembly.GetName().Name + "/Resources/";
        private static readonly string DefaultEmbeddedUrl = EmbeddedResourcesPath + "index.html";
        private static readonly string BuiltinResourcesPath = EmbeddedResourcesPath + "builtin/";
        private static readonly string ReactRootComponentPath = BuiltinResourcesPath + "webview-root-component.js";

        private static readonly Assembly BuiltinResourcesAssembly = Assembly.GetExecutingAssembly();

        private readonly ReactView reactView;
        private readonly IBinder binder;
        private readonly IInterceptor interceptor;

        private string[] componentSource;
        private Assembly userCallingAssembly;

        public InternalReactWebView(ReactView reactView, IBinder binder, IInterceptor interceptor) : base() {
            this.reactView = reactView;
            this.binder = binder;
            this.interceptor = interceptor;
        }

        private static bool IsBuiltinResource(Uri uri) {
            return uri.AbsoluteUri.StartsWith(BuiltinResourcesPath);
        }

        protected override void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
            if (url.AbsoluteUri == ReactRootComponentPath) {
                var rootComponentUrl = EmbeddedScheme + DefaultPath + userCallingAssembly.GetName().Name + "/" + string.Join("/", componentSource); //BuildEmbeddedResourceUrl(userCallingAssembly, new[] { userCallingAssembly.GetName().Name }.Concat(componentSource).ToArray());
                resourceHandler.Redirect(rootComponentUrl);
                return;
            }
            base.LoadEmbeddedResource(resourceHandler, url);
        }

        public void Load(string[] componentSource) {
            this.componentSource = componentSource;
            userCallingAssembly = GetUserCallingAssembly();
            Address = DefaultEmbeddedUrl;
        }

        //protected override Assembly ResolveResourceAssembly(Uri resourceUrl) {
        //    if (resourceUrl.AbsoluteUri == DefaultEmbeddedUrl ||
        //        (IsBuiltinResource(resourceUrl) && resourceUrl.Segments.LastOrDefault() != ReactRootComponentPath)) {
        //        // builtin resources are loaded from current assembly except root component (loaded from calling assembly)
        //        return BuiltinResourcesAssembly;
        //    }
        //    return base.ResolveResourceAssembly(resourceUrl);
        //}

        //protected override string[] ResolveResourcePath(Uri resourceUrl, string assemblyName) {
        //    if (IsBuiltinResource(resourceUrl)) {
        //        if (resourceUrl.Segments.LastOrDefault() == ReactRootComponentPath) {
        //            return (new[] { assemblyName }).Concat(componentSource).ToArray();
        //        }
        //    }
        //    return base.ResolveResourcePath(resourceUrl, assemblyName);

        //    /*if (resourceUrl.AbsoluteUri == DefaultEmbeddedUrl || IsBuiltinResource(resourceUrl)) {
        //        if (resourceUrl.Segments.LastOrDefault() == ReactRootComponentPath) {
        //            // root component is the component specified for this control
        //            return (new[] { assemblyName }).Concat(componentSource).ToArray();
        //        }
        //        return base.ResolveResourcePath(resourceUrl, assemblyName);
        //    }
        //    return (new[] { assemblyName }).Concat(componentSource.Take(componentSource.Length - 1).Concat(resourceUrl.Segments.Skip(1))).ToArray();*/
        //}

        public override void RegisterJavascriptObject(string name, object objectToBind) {
            var options = new BindingOptions() {
                Binder = binder,
                Interceptor = interceptor,
            };
            chromium.RegisterAsyncJsObject(name, objectToBind, options);
        }
    }
}
