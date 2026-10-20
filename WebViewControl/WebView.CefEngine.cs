using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Xilium.CefGlue;
using CefJavascriptStackFrame = Xilium.CefGlue.Common.Events.JavascriptStackFrame;

namespace WebViewControl {

    partial class WebView {

        private partial IWebViewEngine CreateEngine() => new CefEngine(this);

        internal ChromiumBrowser UnderlyingBrowser => ((CefEngine)engine).Browser;

        /// <summary>
        /// Renders the web view with a bundled Chromium (CEF), through CefGlue.
        /// </summary>
        private sealed partial class CefEngine : IWebViewEngine {

            // converts cef zoom percentage to css zoom (between 0 and 1)
            // from https://code.google.com/p/chromium/issues/detail?id=71484
            private const float PercentageToZoomFactor = 1.2f;

            public CefEngine(WebView owner) {
                Browser = new ChromiumBrowser();
                Browser.BrowserInitialized += owner.HandleBrowserInitialized;
                Browser.LoadEnd += (o, e) => owner.HandleLoadEnd(new CefJavascriptFrame(Browser, e.Frame));
                Browser.LoadError += (o, e) => owner.HandleLoadError(e.FailedUrl, (int)e.ErrorCode, e.ErrorCode == CefErrorCode.Aborted, e.Frame.Name, e.Frame.IsMain);
                Browser.TitleChanged += delegate { owner.HandleTitleChanged(); };
                Browser.AddressChanged += (o, address) => owner.HandleAddressChanged(address);
                Browser.JavascriptContextCreated += (o, e) => owner.HandleJavascriptContextCreated(new CefJavascriptFrame(Browser, e.Frame));
                Browser.JavascriptContextReleased += (o, e) => owner.HandleJavascriptContextReleased(e.Frame.Name, e.Frame.Url);
                Browser.JavascriptUncaughException += (o, e) => owner.HandleJavascriptUncaughtException(e.Message, ToJavascriptStackFrames(e.StackFrames), e.Frame.Name);
                Browser.UnhandledException += (o, e) => owner.HandleUnhandledException(e.Exception);

                Browser.RequestHandler = new InternalRequestHandler(owner);
                Browser.LifeSpanHandler = new InternalLifeSpanHandler(owner);
                Browser.ContextMenuHandler = new InternalContextMenuHandler(owner);
                Browser.DialogHandler = new InternalDialogHandler(owner);
                Browser.DownloadHandler = new InternalDownloadHandler(owner);
                Browser.JSDialogHandler = new InternalJsDialogHandler(owner);
                Browser.DragHandler = new InternalDragHandler(owner);
                Browser.KeyboardHandler = new InternalKeyboardHandler(owner);

                if (!Settings.OsrEnabled) {
                    // having the handler (by default) seems to cause some focus troubles, enable only when osr disabled
                    Browser.FocusHandler = new InternalFocusHandler(owner);
                }
            }

            public ChromiumBrowser Browser { get; }

            public bool IsBrowserInitialized => Browser.IsBrowserInitialized;

            public bool IsJavascriptEngineInitialized => Browser.IsJavascriptEngineInitialized;

            public bool IsLoading => Browser.IsLoading;

            public string Address => Browser.Address;

            public string Title => Browser.Title;

            public bool CanGoBack => Browser.CanGoBack;

            public bool CanGoForward => Browser.CanGoForward;

            public double ZoomPercentage {
                get => Math.Pow(PercentageToZoomFactor, Browser.ZoomLevel);
                set => Browser.ZoomLevel = Math.Log(value, PercentageToZoomFactor);
            }

            public void CreateBrowser(int width, int height) => Browser.CreateBrowser(width, height);

            public void Navigate(string url) => Browser.Address = url;

            public void GoBack() => Browser.GoBack();

            public void GoForward() => Browser.GoForward();

            public void Reload(bool ignoreCache) => Browser.Reload(ignoreCache);

            public void ShowDeveloperTools() => Browser.ShowDeveloperTools();

            public void CloseDeveloperTools() => Browser.CloseDeveloperTools();

            public void SetAccessibilityState(bool enabled) =>
                Browser.GetBrowser()?.GetHost()?.SetAccessibilityState(enabled ? CefState.Enabled : CefState.Disabled);

            public void ExecuteEditCommand(EditCommand command) {
                var frame = Browser.GetBrowser()?.GetFocusedFrame() ?? Browser.GetBrowser()?.GetMainFrame();
                switch (command) {
                    case EditCommand.Cut:
                        frame?.Cut();
                        break;
                    case EditCommand.Copy:
                        frame?.Copy();
                        break;
                    case EditCommand.Paste:
                        frame?.Paste();
                        break;
                    case EditCommand.SelectAll:
                        frame?.SelectAll();
                        break;
                    case EditCommand.Undo:
                        frame?.Undo();
                        break;
                    case EditCommand.Redo:
                        frame?.Redo();
                        break;
                    case EditCommand.Delete:
                        frame?.Delete();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown edit command");
                }
            }

            public void Focus() => Browser.Focus();

            public IJavascriptFrame GetFrame(string frameName) {
                var frame = Browser.GetBrowser()?.GetFrame(frameName ?? "");
                return frame == null ? null : new CefJavascriptFrame(Browser, frame);
            }

            public string[] GetFrameNames() => Browser.GetBrowser()?.GetFrameNames() ?? new string[0];

            public bool IsJavascriptObjectRegistered(string name) => Browser.IsJavascriptObjectRegistered(name);

            public void RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall) =>
                Browser.RegisterJavascriptObject(objectToBind, name, target => interceptCall(target));

            public void UnregisterJavascriptObject(string name) => Browser.UnregisterJavascriptObject(name);

            public void Dispose() => Browser.Dispose();

            private static JavascriptStackFrame[] ToJavascriptStackFrames(CefJavascriptStackFrame[] stackFrames) =>
                stackFrames?.Select(f => new JavascriptStackFrame(f.FunctionName, f.ScriptNameOrSourceUrl, f.Column, f.LineNumber)).ToArray();
        }

        private sealed class CefJavascriptFrame : IJavascriptFrame {

            private readonly ChromiumBrowser browser;
            private readonly CefFrame frame;

            public CefJavascriptFrame(ChromiumBrowser browser, CefFrame frame) {
                this.browser = browser;
                this.frame = frame;
            }

            public string Name => frame.Name;

            public string Url => frame.Url;

            public bool IsMain => frame.IsMain;

            public bool IsValid => frame.IsValid;

            // scripts are evaluated as they were before the engine seam: through the browser, which targets the main frame
            public Task<T> EvaluateScript<T>(string script, TimeSpan? timeout) => browser.EvaluateJavaScript<T>(script, timeout: timeout);

            public void LoadUrl(string url) => frame.LoadUrl(url);
        }

        private sealed class CefRequestDetails : IRequestDetails {

            private readonly CefRequest request;

            public CefRequestDetails(CefRequest request) {
                this.request = request;
            }

            public string Method => request.Method;

            public string Url => request.Url;

            public ResourceType ResourceType => (ResourceType)request.ResourceType;

            public NameValueCollection GetHeaderMap() => request.GetHeaderMap();

            public void SetHeaderMap(NameValueCollection headers) => request.SetHeaderMap(headers);

            public string GetHeaderByName(string name) => request.GetHeaderByName(name);

            public void SetHeaderByName(string name, string value, bool overwrite) => request.SetHeaderByName(name, value, overwrite);
        }
    }

    internal static partial class WebViewExtensions {

        internal static void SendKeyEvent(this WebView webview, CefKeyEvent keyEvent) {
            webview.UnderlyingBrowser.GetBrowser()?.GetHost()?.SendKeyEvent(keyEvent);
        }
    }
}
