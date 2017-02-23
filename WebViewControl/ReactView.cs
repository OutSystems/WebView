using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace WebViewControl {

    public class ReactView : ContentControl {

        private class InternalReactWebView : WebView {

            private const string ReactRootComponentPath = "webview-root-component.js";

            private static readonly Assembly BuiltinResourcesAssembly = Assembly.GetExecutingAssembly();

            private readonly string[] componentSource;

            public InternalReactWebView(Assembly assembly, params string[] source) : base() {
                componentSource = source;
                LoadFrom(assembly, componentSource.First());
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

        private readonly InternalReactWebView reactWebView;

        public ReactView(params string[] source) : base() {
            reactWebView = new InternalReactWebView(Assembly.GetCallingAssembly(), source);
            Content = reactWebView;
        }

        public object NativeApi {
            set { reactWebView.RegisterJavascriptObject("NativeApi", value); }
        }
    }
}
