using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WebViewControl;

namespace ReactViewControl {

    partial class ReactViewRender {

        internal class LoaderModule {

            private const string LoaderModuleName = "Loader";

            private class SyncFunction {
                public static readonly SyncFunction Instance = new SyncFunction();
                private SyncFunction() { }
            }

            public LoaderModule(ReactViewRender viewRender) {
                ViewRender = viewRender;
            }

            private ReactViewRender ViewRender { get; }

            /// <summary>
            /// Loads the specified react component into the specified frame
            /// </summary>
            public void LoadComponent(IViewModule component, string frameName, bool hasStyleSheet, bool hasPlugins, bool forceNativeSyncCalls) {
                var mainSource = ViewRender.ToFullUrl(NormalizeUrl(component.MainJsSource));
                var dependencySources = component.DependencyJsSources.Select(s => ViewRender.ToFullUrl(NormalizeUrl(s))).ToArray();
                var cssSources = component.CssSources.Select(s => ViewRender.ToFullUrl(NormalizeUrl(s))).ToArray();

                var componentSerialization = SerializeComponent(component, forceNativeSyncCalls);
                var componentHash = ComputeHash(componentSerialization);

                // loadComponent arguments:
                //
                // componentName: string,
                // componentNativeObjectName: string,
                // componentSource: string,
                // dependencySources: string[],
                // cssSources: string[],
                // maxPreRenderedCacheEntries: number,
                // hasStyleSheet: boolean,
                // hasPlugins: boolean,
                // componentNativeObject: Dictionary<any>,
                // frameName: string
                // componentHash: string

                var loadArgs = new[] {
                    JavascriptSerializer.Serialize(component.Name),
                    JavascriptSerializer.Serialize(component.GetNativeObjectFullName(frameName)),
                    JavascriptSerializer.Serialize(mainSource),
                    JavascriptSerializer.Serialize(dependencySources),
                    JavascriptSerializer.Serialize(cssSources),
                    JavascriptSerializer.Serialize(ReactView.PreloadedCacheEntriesSize),
                    JavascriptSerializer.Serialize(hasStyleSheet),
                    JavascriptSerializer.Serialize(hasPlugins),
                    componentSerialization,
                    JavascriptSerializer.Serialize(frameName),
                    JavascriptSerializer.Serialize(componentHash),
                };

                ExecuteLoaderFunction("loadComponent", loadArgs);
            }

            /// <summary>
            /// Loads the specified stylesheet.
            /// </summary>
            /// <param name="stylesheet"></param>
            public void LoadDefaultStyleSheet(ResourceUrl stylesheet) {
                ExecuteLoaderFunction("loadDefaultStyleSheet", SerializeResourceUrl(stylesheet));
            }

            /// <summary>
            /// Set the specified stylesheet.
            /// </summary>
            /// <param name="stylesheet"></param>
            public void SetDefaultStyleSheet(ResourceUrl stylesheet) {
                ExecuteLoaderFunction("setDefaultStyleSheet", SerializeResourceUrl(stylesheet));
            }

            /// <summary>
            /// Loads the specified plugins modules in the specified frame.
            /// </summary>
            /// <param name="plugins"></param>
            /// <param name="frameName"></param>
            public void LoadPlugins(IViewModule[] plugins, string frameName) {
                var loadArgs = new[] {
                    JavascriptSerializer.Serialize(plugins.Select(m => new object[] {
                        m.Name, // plugin name
                        ViewRender.ToFullUrl(NormalizeUrl(m.MainJsSource)), // plugin source
                        m.GetNativeObjectFullName(frameName), // native object name
                        m.DependencyJsSources.Select(s => ViewRender.ToFullUrl(NormalizeUrl(s))) // plugin dependencies
                    })),
                    JavascriptSerializer.Serialize(frameName)
                };

                ExecuteLoaderFunction("loadPlugins", loadArgs);
            }

            /// <summary>
            /// Shows an resource load error message for the spcified url.
            /// </summary>
            /// <param name="url"></param>
            public void ShowResourceLoadFailedMessage(string url) {
                ShowErrorMessage($"Failed to load resource '{url}'. Press F12 to open developer tools and see more details.");
            }

            /// <summary>
            /// Shows the specified error message.
            /// </summary>
            /// <param name="msg"></param>
            public void ShowErrorMessage(string msg) {
                msg = msg.Replace("\"", "\\\"");
                ExecuteLoaderFunction("showErrorMessage", JavascriptSerializer.Serialize(msg));
            }

            /// <summary>
            /// Prevents mouse interaction with the browser
            /// </summary>
            /// <param name="disable"></param>
            public void DisableInputInteractions(bool disable) {
                ExecuteLoaderFunction("disableInputInteractions", JavascriptSerializer.Serialize(disable));
            }

            /// <summary>
            /// Executes the specified javascript function on the Loader module.
            /// </summary>
            /// <param name="functionName"></param>
            /// <param name="args"></param>
            private void ExecuteLoaderFunction(string functionName, params string[] args) {
                // using setimeout we make sure the function is already defined
                var loaderUrl = new ResourceUrl(ResourcesAssembly, ReactViewResources.Resources.LoaderUrl);
                ViewRender.WebView.ExecuteScript($"import('{loaderUrl}').then(m => m.default.{LoaderModuleName}).then({LoaderModuleName} => {LoaderModuleName}.{functionName}({string.Join(",", args)}))");
            }

            private static string SerializeComponent(IViewModule component, bool forceNativeSyncCalls) {
                var nativeObjectMethodsMap = component.Events
                    .Select(g => new KeyValuePair<string, object>(g, forceNativeSyncCalls ? SyncFunction.Instance : JavascriptSerializer.Undefined))
                    .Concat(component.PropertiesValues)
                    .OrderBy(p => p.Key)
                    .Select(p => new KeyValuePair<string, object>(JavascriptSerializer.GetJavascriptName(p.Key), p.Value));
                return JavascriptSerializer.Serialize(nativeObjectMethodsMap, o => o == SyncFunction.Instance ? $"{LoaderModuleName}.syncFunction" : JavascriptSerializer.Serialize(o));
            }

            private static string ComputeHash(string inputString) {
                using (var sha256 = SHA256.Create()) {
                    return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString)));
                }
            }

            private string SerializeResourceUrl(ResourceUrl resource) {
                return JavascriptSerializer.Serialize(NormalizeUrl(ViewRender.ToFullUrl(resource.ToString())));
            }
        }
    }
}
