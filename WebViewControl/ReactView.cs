using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using CefSharp;
using CefSharp.ModelBinding;

namespace WebViewControl {

    public class ReactView : ContentControl {

        public struct TrackCode {
            public long Value;
        }

        public class ViewObject {
            public TrackCode TrackCode;
        }

        private class JsObjectInterceptor : IInterceptor {

            private readonly ReactView reactView;

            public JsObjectInterceptor(ReactView reactView) {
                this.reactView = reactView;
            }

            public object Intercept(Func<object> originalMethod) {
                object result = originalMethod();
                if (result != null) {
                    if (result is IEnumerable) {
                        var objects = (IEnumerable) result;
                        foreach (object item in objects) {
                            reactView.TrackObject(item);
                        }
                    } else {
                        reactView.TrackObject(result);
                    }
                }
                return result;
            }
        }

        private class JsObjectBinder : DefaultBinder {

            private readonly ReactView reactView;

            public JsObjectBinder(ReactView reactView) : base(new DefaultFieldNameConverter()) {
                this.reactView = reactView;
            }

            public override object Bind(object obj, Type modelType) {
                if (modelType.IsInterface) {
                    // TODO check if its a ViewObject
                    // TODO int .. might not be enough
                    // TODO trackcode
                    var trackCode = (int)((Dictionary<string, object>)obj)["Value"];
                    return reactView.GetTrackedObject(trackCode);
                }
                return base.Bind(obj, modelType);
            }
        }

        private class InternalReactWebView : WebView {

            private const string ReactRootComponentPath = "webview-root-component.js";

            private static readonly Assembly BuiltinResourcesAssembly = Assembly.GetExecutingAssembly();

            private readonly ReactView reactView;
            private string[] componentSource;

            public InternalReactWebView(ReactView reactView) : base() {
                this.reactView = reactView;
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
                    Binder = reactView.binder,
                    Interceptor = reactView.interceptor,
                };
                chromium.RegisterAsyncJsObject(name, objectToBind, options);
            }
        }

        private readonly InternalReactWebView reactWebView;
        private readonly Dictionary<long, object> jsObjects = new Dictionary<long, object>();
        private readonly JsObjectBinder binder;
        private readonly JsObjectInterceptor interceptor;

        private long objectCounter;
        private string source;

        public ReactView() {
            reactWebView = new InternalReactWebView(this);
            reactWebView.AllowDeveloperTools = true;
            reactWebView.DisableBuiltinContextMenus = true;
            Content = reactWebView;

            binder = new JsObjectBinder(this);
            interceptor = new JsObjectInterceptor(this);
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

        private object GetTrackedObject(long id) {
            object obj;
            if (jsObjects.TryGetValue(id, out obj)) {
                return obj;
            }
            throw new InvalidOperationException("Unknown object with track code:");
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

        private static bool IsFrameworkAssemblyName(string name) {
            return name == "PresentationFramework" || name == "mscorlib" || name == "System.Xaml";
        }
    }
}
