using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace WebViewControl {

    /// <summary>
    /// Renders the web view with WebView2, the Edge (Chromium) runtime of Windows.
    /// - the custom schemes (embedded, custom, local) are registered in the environment and served from WebResourceRequested
    /// - the javascript bridge talks through chrome.webview.postMessage (page to .NET) and PostWebMessageAsJson (.NET to page)
    /// - the browser renders in a child window, which Avalonia hosts
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WebView2Engine : NativeWebViewEngine {

        private const int VirtualKeyF12 = 0x7B;

        private static readonly string[] Schemes = { ResourceUrl.EmbeddedScheme, ResourceUrl.CustomScheme, ResourceUrl.LocalScheme };

        // every web view of the process shares the same environment (browser process, user data folder and schemes)
        private static Task<CoreWebView2Environment> environmentTask;

        private IntPtr window;
        private CoreWebView2Environment environment;
        private CoreWebView2Controller controller;
        private CoreWebView2 webView;
        private string mainFrameNavigationUrl;

        public WebView2Engine(WebView owner) : base(owner) { }

        internal override string NativeViewHandleDescriptor => "HWND";

        public override bool CanGoBack => webView?.CanGoBack == true;

        public override bool CanGoForward => webView?.CanGoForward == true;

        protected override IntPtr CreateNativeView(int width, int height) {
            if (window == IntPtr.Zero) {
                window = Win32.CreateChildWindow(width, height, OnWindowMessage);
                CreateBrowserAsync();
            }
            return window;
        }

        private async void CreateBrowserAsync() {
            try {
                environment = await GetEnvironmentAsync();
                var newController = await environment.CreateCoreWebView2ControllerAsync(window);
                if (IsDisposed) {
                    newController.Close();
                    return;
                }

                controller = newController;
                webView = controller.CoreWebView2;
                controller.DefaultBackgroundColor = WebView.Settings.BackgroundColor;

                ConfigureSettings();

                // the bridge is installed in the main frame before any script of the page runs
                await webView.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);
                foreach (var scheme in Schemes) {
                    webView.AddWebResourceRequestedFilter(scheme + "://*", CoreWebView2WebResourceContext.All);
                }

                SubscribeEvents();

                UpdateBounds();
                controller.IsVisible = true;

                OnBrowserCreated();
            } catch (Exception e) {
                OnBrowserCreationFailed(e);
            }
        }

        private static Task<CoreWebView2Environment> GetEnvironmentAsync() {
            return environmentTask ??= CreateEnvironmentAsync();
        }

        private static Task<CoreWebView2Environment> CreateEnvironmentAsync() {
            var settings = WebView.Settings;

            var arguments = settings.CommandLineSwitches.Select(s => string.IsNullOrEmpty(s.Value) ? $"--{s.Key}" : $"--{s.Key}={s.Value}").ToList();
            var remoteDebuggingPort = settings.GetRemoteDebuggingPort();
            if (remoteDebuggingPort > 0) {
                arguments.Add($"--remote-debugging-port={remoteDebuggingPort}");
            }

            var schemeRegistrations = Schemes.Select(scheme => new CoreWebView2CustomSchemeRegistration(scheme) {
                TreatAsSecure = true,
                HasAuthorityComponent = true,
                // the pages of the custom schemes fetch resources across them (eg: embedded and custom)
                AllowedOrigins = new List<string> { "*" }
            }).ToList();

            var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: string.Join(" ", arguments), customSchemeRegistrations: schemeRegistrations);

            // the user data folder holds the cache, as CEF's cache path does
            return CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: settings.CachePath, options: options);
        }

        private void ConfigureSettings() {
            var settings = webView.Settings;
            settings.AreDevToolsEnabled = Owner.AllowDeveloperTools;
            settings.AreDefaultContextMenusEnabled = !Owner.DisableBuiltinContextMenus;
            settings.IsStatusBarEnabled = false;
            settings.AreHostObjectsAllowed = false;
            // no find, print, reload or zoom shortcuts: like with CEF, the hosting application owns the keyboard shortcuts
            settings.AreBrowserAcceleratorKeysEnabled = false;
            if (!string.IsNullOrEmpty(WebView.Settings.UserAgent)) {
                settings.UserAgent = WebView.Settings.UserAgent;
            }
        }

        private void SubscribeEvents() {
            webView.NavigationStarting += OnNavigationStarting;
            webView.FrameNavigationStarting += OnFrameNavigationStarting;
            webView.ContentLoading += OnContentLoading;
            webView.NavigationCompleted += OnNavigationCompleted;
            webView.SourceChanged += OnSourceChanged;
            webView.DocumentTitleChanged += OnDocumentTitleChanged;
            webView.WebMessageReceived += OnWebMessageReceived;
            webView.WebResourceRequested += OnWebResourceRequested;
            webView.NewWindowRequested += OnNewWindowRequested;
            webView.ProcessFailed += OnProcessFailed;
            webView.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
            controller.AcceleratorKeyPressed += OnAcceleratorKeyPressed;
            controller.GotFocus += OnControllerGotFocus;
            controller.MoveFocusRequested += OnMoveFocusRequested;
        }

        protected override void DestroyNativeBrowser() {
            if (controller != null) {
                controller.Close();
                controller = null;
                webView = null;
            }
            if (window != IntPtr.Zero) {
                Win32.DestroyChildWindow(window);
                window = IntPtr.Zero;
            }
        }

        protected override void NavigateTo(string url) {
            try {
                webView.Navigate(url);
            } catch (ArgumentException) {
                OnNavigationFailed(url, (int)CoreWebView2WebErrorStatus.Unknown, isAborted: false);
            }
        }

        protected override void GoBackCore() => webView.GoBack();

        protected override void GoForwardCore() => webView.GoForward();

        protected override void ReloadCore(bool ignoreCache) {
            if (ignoreCache) {
                _ = CallDevToolsProtocolMethodAsync("Page.reload", "{\"ignoreCache\":true}");
            } else {
                webView.Reload();
            }
        }

        protected override void ShowDeveloperToolsCore() {
            webView.Settings.AreDevToolsEnabled = true;
            webView.OpenDevToolsWindow();
        }

        protected override void CloseDeveloperToolsCore() {
            // WebView2 has no api to close the developer tools window
        }

        protected override void ExecuteEditCommandCore(EditCommand command) {
            var editorCommand = command switch {
                EditCommand.Cut => "Cut",
                EditCommand.Copy => "Copy",
                EditCommand.Paste => "Paste",
                EditCommand.SelectAll => "SelectAll",
                EditCommand.Undo => "Undo",
                EditCommand.Redo => "Redo",
                EditCommand.Delete => "Delete",
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown edit command")
            };
            _ = ExecuteEditorCommandAsync(editorCommand);
        }

        private async Task ExecuteEditorCommandAsync(string editorCommand) {
            // runs the editor command natively, as CEF's frame commands do (document.execCommand can't paste)
            await CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", $"{{\"type\":\"rawKeyDown\",\"commands\":[\"{editorCommand}\"]}}");
            await CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\"}");
        }

        protected override void FocusCore() => controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);

        protected override void SetZoom(double percentage) => controller.ZoomFactor = percentage;

        protected override void PostMessageToPage(string json) => webView?.PostWebMessageAsJson(json);

        private async Task CallDevToolsProtocolMethodAsync(string method, string parameters) {
            try {
                if (webView != null) {
                    await webView.CallDevToolsProtocolMethodAsync(method, parameters);
                }
            } catch (Exception e) {
                Trace.TraceWarning("WebView: {0} failed: {1}", method, e.Message);
            }
        }

        private void OnWindowMessage(uint message) {
            switch (message) {
                case Win32.WM_SIZE:
                    UpdateBounds();
                    break;
                case Win32.WM_MOVE:
                case Win32.WM_WINDOWPOSCHANGED:
                    controller?.NotifyParentWindowPositionChanged();
                    break;
                case Win32.WM_SETFOCUS:
                    controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                    break;
            }
        }

        private void UpdateBounds() {
            if (controller != null && Win32.GetClientRect(window, out var rect)) {
                controller.Bounds = new Rectangle(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
            if (OnBeforeNavigate(e.Uri, "GET", isMainFrame: true, isBackOrForward: e.NavigationKind == CoreWebView2NavigationKind.BackOrForward)) {
                e.Cancel = true;
                return;
            }
            mainFrameNavigationUrl = e.Uri;
            OnNavigationStarted();
        }

        private void OnFrameNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
            if (OnBeforeNavigate(e.Uri, "GET", isMainFrame: false, isBackOrForward: false)) {
                e.Cancel = true;
            }
        }

        private void OnContentLoading(object sender, CoreWebView2ContentLoadingEventArgs e) => OnNavigationCommitted();

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            if (e.IsSuccess) {
                OnNavigationCompleted(webView.Source);
            } else {
                OnNavigationFailed(mainFrameNavigationUrl ?? webView.Source, (int)e.WebErrorStatus, isAborted: e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled);
            }
        }

        private void OnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e) => OnAddressChanged(webView.Source);

        private void OnDocumentTitleChanged(object sender, object e) => OnTitleChanged(webView.DocumentTitle);

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            string message;
            try {
                message = e.TryGetWebMessageAsString();
            } catch (ArgumentException) {
                // not a message of the bridge (which only posts strings)
                return;
            }
            OnMessageReceived(message);
        }

        private async void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e) {
            var deferral = e.GetDeferral();
            NativeResourceResult result;
            try {
                var request = new NativeRequestDetails(e.Request.Uri, e.Request.Method, GetResourceType(e.ResourceContext, e.Request.Uri), GetHeaders(e.Request.Headers));
                result = await GetResourceAsync(request).ConfigureAwait(false);
            } catch (Exception exception) {
                Trace.TraceError("WebView: {0}", exception);
                result = NativeResourceResult.NotFound;
            }
            RunOnUIThread(() => {
                try {
                    if (!IsDisposed && environment != null) {
                        e.Response = environment.CreateWebResourceResponse(new MemoryStream(result.Content), result.StatusCode, result.ReasonPhrase, FormatHeaders(result.GetHeaders()));
                    }
                } finally {
                    deferral.Complete();
                }
            });
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e) {
            if (OnBeforePopup(e.Uri, webView.Source)) {
                e.Handled = true;
            }
        }

        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e) {
            if (e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessExited && e.ProcessFailedKind != CoreWebView2ProcessFailedKind.BrowserProcessExited) {
                // other processes (gpu, utilities, iframes) are restarted by WebView2
                return;
            }
            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited) {
                // the environment is gone with its browser process: the next web views start a new one
                environmentTask = null;
            }
            var status = e.Reason switch {
                CoreWebView2ProcessFailedReason.OutOfMemory => RenderProcessTerminationStatus.OutOfMemory,
                CoreWebView2ProcessFailedReason.Terminated => RenderProcessTerminationStatus.Killed,
                CoreWebView2ProcessFailedReason.Crashed => RenderProcessTerminationStatus.Crashed,
                CoreWebView2ProcessFailedReason.Unexpected => RenderProcessTerminationStatus.Crashed,
                _ => RenderProcessTerminationStatus.Unknown
            };
            OnRenderProcessTerminated(status);
        }

        private void OnServerCertificateErrorDetected(object sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e) {
            if (Owner.IgnoreCertificateErrors) {
                e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
            }
        }

        private void OnAcceleratorKeyPressed(object sender, CoreWebView2AcceleratorKeyPressedEventArgs e) {
            if (e.KeyEventKind != CoreWebView2KeyEventKind.KeyDown && e.KeyEventKind != CoreWebView2KeyEventKind.SystemKeyDown) {
                return;
            }
            if (e.VirtualKey == VirtualKeyF12 && Owner.AllowDeveloperTools) {
                e.Handled = true;
                ShowDeveloperToolsCore();
                return;
            }
            // only accelerator keys (eg: with ctrl or alt, function keys) reach this event
            if (Owner.HasKeyPressedHandler && Owner.HandleKeyPressed(new KeyEventDetails((int)e.VirtualKey, '\0'))) {
                e.Handled = true;
            }
        }

        private void OnControllerGotFocus(object sender, object e) => Owner.HandleGotFocus();

        private void OnMoveFocusRequested(object sender, CoreWebView2MoveFocusRequestedEventArgs e) => Owner.HandleLostFocus();

        private ResourceType GetResourceType(CoreWebView2WebResourceContext context, string url) {
            switch (context) {
                case CoreWebView2WebResourceContext.Document:
                    return url == mainFrameNavigationUrl ? ResourceType.MainFrame : ResourceType.SubFrame;
                case CoreWebView2WebResourceContext.Stylesheet:
                    return ResourceType.Stylesheet;
                case CoreWebView2WebResourceContext.Script:
                    return ResourceType.Script;
                case CoreWebView2WebResourceContext.Image:
                    return ResourceType.Image;
                case CoreWebView2WebResourceContext.Font:
                    return ResourceType.FontResource;
                case CoreWebView2WebResourceContext.XmlHttpRequest:
                case CoreWebView2WebResourceContext.Fetch:
                    return ResourceType.Xhr;
                default:
                    return ResourceType.SubResource;
            }
        }

        private static NameValueCollection GetHeaders(IEnumerable<KeyValuePair<string, string>> headers) {
            var result = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers) {
                result[header.Key] = header.Value;
            }
            return result;
        }

        private static string FormatHeaders(NameValueCollection headers) {
            var result = new StringBuilder();
            foreach (string name in headers) {
                result.Append(name).Append(": ").Append(headers[name]).Append("\r\n");
            }
            return result.ToString();
        }
    }
}
