using System;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    public class ReactWebView : WebView {

        private const string ReactRootComponentPath = "webview-root-component.js";

        private static readonly Assembly BuiltinResourcesAssembly = Assembly.GetExecutingAssembly();

        private readonly string[] componentSource;

        public ReactWebView(params string[] source) : base() {
            componentSource = source;
            LoadFrom(Assembly.GetCallingAssembly(), componentSource.First());
        }

        private static bool IsBuiltinResource(Uri uri) {
            return uri.Segments.Length > 1 && uri.Segments[1] == BuiltinResourcesPath;
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
    }
}
