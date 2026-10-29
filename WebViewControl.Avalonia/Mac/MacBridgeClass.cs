using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static WebViewControl.Native.Mac.Objc;

namespace WebViewControl.Native.Mac {

    /// <summary>
    /// The objective-c class (registered at runtime) that WebKit talks to. One instance is created per web view and is simultaneously:
    ///  - the WKURLSchemeHandler that serves the embedded/custom resources
    ///  - the WKScriptMessageHandler that receives the messages posted by the page (see NativeBridge.js)
    ///  - the WKNavigationDelegate and the WKUIDelegate
    /// Every callback is forwarded to the <see cref="MacBrowser"/> the instance was created for.
    /// </summary>
    internal static unsafe class MacBridgeClass {

        private const string ClassName = "RVNativeWebViewBridge";

        private static readonly object InitializationLock = new object();
        private static readonly ConcurrentDictionary<IntPtr, MacBrowser> Instances = new ConcurrentDictionary<IntPtr, MacBrowser>();

        private static IntPtr bridgeClass;

        public static IntPtr CreateInstance(MacBrowser owner) {
            EnsureClassRegistered();
            var instance = Send(Send(bridgeClass, Sel("alloc")), Sel("init"));
            Instances[instance] = owner;
            return instance;
        }

        public static void DestroyInstance(IntPtr instance) {
            if (Instances.TryRemove(instance, out _)) {
                Release(instance);
            }
        }

        private static void EnsureClassRegistered() {
            lock (InitializationLock) {
                if (bridgeClass != IntPtr.Zero) {
                    return;
                }

                // WebKit is not loaded in the process unless we load it
                dlopen("/System/Library/Frameworks/WebKit.framework/WebKit", 2 /* RTLD_NOW */);

                var existing = Cls(ClassName);
                if (existing != IntPtr.Zero) {
                    bridgeClass = existing;
                    return;
                }

                bridgeClass = new ObjcClassBuilder(ClassName)
                    .AddProtocol("WKURLSchemeHandler")
                    .AddProtocol("WKScriptMessageHandler")
                    .AddProtocol("WKNavigationDelegate")
                    .AddProtocol("WKUIDelegate")
                    .AddMethod("webView:startURLSchemeTask:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&StartUrlSchemeTask, "v@:@@")
                    .AddMethod("webView:stopURLSchemeTask:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&StopUrlSchemeTask, "v@:@@")
                    .AddMethod("userContentController:didReceiveScriptMessage:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidReceiveScriptMessage, "v@:@@")
                    .AddMethod("webView:decidePolicyForNavigationAction:decisionHandler:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DecidePolicyForNavigationAction, "v@:@@@?")
                    .AddMethod("webView:didCommitNavigation:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidCommitNavigation, "v@:@@")
                    .AddMethod("webView:didFinishNavigation:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFinishNavigation, "v@:@@")
                    .AddMethod("webView:didFailNavigation:withError:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFailNavigation, "v@:@@@")
                    .AddMethod("webView:didFailProvisionalNavigation:withError:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFailNavigation, "v@:@@@")
                    .AddMethod("webView:didReceiveAuthenticationChallenge:completionHandler:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidReceiveAuthenticationChallenge, "v@:@@@?")
                    .AddMethod("webViewWebContentProcessDidTerminate:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&ContentProcessDidTerminate, "v@:@")
                    .AddMethod("webView:createWebViewWithConfiguration:forNavigationAction:windowFeatures:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)&CreateWebView, "@@:@@@@")
                    .AddMethod("webView:runJavaScriptAlertPanelWithMessage:initiatedByFrame:completionHandler:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&RunAlertPanel, "v@:@@@@?")
                    .AddMethod("webView:runJavaScriptConfirmPanelWithMessage:initiatedByFrame:completionHandler:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&RunConfirmPanel, "v@:@@@@?")
                    .AddMethod("webView:runJavaScriptTextInputPanelWithPrompt:defaultText:initiatedByFrame:completionHandler:", (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&RunTextInputPanel, "v@:@@@@@?")
                    .Register();
            }
        }

        private static MacBrowser Owner(IntPtr self) => Instances.TryGetValue(self, out var browser) ? browser : null;

        // ---- WKURLSchemeHandler ---------------------------------------------------------------------------------------

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void StartUrlSchemeTask(IntPtr self, IntPtr selector, IntPtr webView, IntPtr task) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnSchemeTaskStarted(task));
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void StopUrlSchemeTask(IntPtr self, IntPtr selector, IntPtr webView, IntPtr task) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnSchemeTaskStopped(task));
        }

        // ---- WKScriptMessageHandler -----------------------------------------------------------------------------------

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DidReceiveScriptMessage(IntPtr self, IntPtr selector, IntPtr userContentController, IntPtr message) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnScriptMessage(FromNSString(Send(message, Sel("body")))));
        }

        // ---- WKNavigationDelegate -------------------------------------------------------------------------------------

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DecidePolicyForNavigationAction(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigationAction, IntPtr decisionHandler) {
            const long Cancel = 0;
            const long Allow = 1;

            var cancel = false;
            var browser = Owner(self);
            browser?.Guard(() => {
                var request = Send(navigationAction, Sel("request"));
                var url = FromNSUrl(Send(request, Sel("URL")));
                var method = FromNSString(Send(request, Sel("HTTPMethod")));
                var targetFrame = Send(navigationAction, Sel("targetFrame"));
                // no target frame means that the navigation would open a new window
                var isMainFrame = targetFrame == IntPtr.Zero || SendBool(targetFrame, Sel("isMainFrame")) != 0;
                const long WKNavigationTypeBackForward = 2;
                var isBackForward = SendLong(navigationAction, Sel("navigationType")) == WKNavigationTypeBackForward;
                cancel = browser.OnNavigationRequested(url, method, isMainFrame, isBackForward);
            });

            // the decision handler must always be called, exactly once
            Block.Call(decisionHandler, cancel ? Cancel : Allow);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DidCommitNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnNavigationCommitted());
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DidFinishNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnNavigationFinished());
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DidFailNavigation(IntPtr self, IntPtr selector, IntPtr webView, IntPtr navigation, IntPtr error) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnNavigationFailed(
                (int)SendLong(error, Sel("code")),
                FromNSString(Send(error, Sel("localizedDescription"))),
                FromNSUrl(Send(Send(error, Sel("userInfo")), Sel("objectForKey:"), NSString("NSErrorFailingURLKey")))));
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void DidReceiveAuthenticationChallenge(IntPtr self, IntPtr selector, IntPtr webView, IntPtr challenge, IntPtr completionHandler) {
            const long PerformDefaultHandling = 1; // NSURLSessionAuthChallengePerformDefaultHandling

            var disposition = PerformDefaultHandling;
            var credential = IntPtr.Zero;
            var browser = Owner(self);
            browser?.Guard(() => (disposition, credential) = browser.OnAuthenticationChallenge(challenge));

            // the completion handler must always be called, exactly once
            Block.Call(completionHandler, disposition, credential);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void ContentProcessDidTerminate(IntPtr self, IntPtr selector, IntPtr webView) {
            var browser = Owner(self);
            browser?.Guard(() => browser.OnContentProcessTerminated());
        }

        // ---- WKUIDelegate ---------------------------------------------------------------------------------------------

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static IntPtr CreateWebView(IntPtr self, IntPtr selector, IntPtr webView, IntPtr configuration, IntPtr navigationAction, IntPtr windowFeatures) {
            var browser = Owner(self);
            browser?.Guard(() => {
                var url = FromNSUrl(Send(Send(navigationAction, Sel("request")), Sel("URL")));
                browser.OnPopupRequested(url);
            });
            // never create the popup, the owner decides what to do with the url
            return IntPtr.Zero;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void RunAlertPanel(IntPtr self, IntPtr selector, IntPtr webView, IntPtr message, IntPtr frame, IntPtr completionHandler) {
            RunPanel(self, JavascriptDialogKind.Alert, message, IntPtr.Zero, completionHandler);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void RunConfirmPanel(IntPtr self, IntPtr selector, IntPtr webView, IntPtr message, IntPtr frame, IntPtr completionHandler) {
            RunPanel(self, JavascriptDialogKind.Confirm, message, IntPtr.Zero, completionHandler);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void RunTextInputPanel(IntPtr self, IntPtr selector, IntPtr webView, IntPtr prompt, IntPtr defaultText, IntPtr frame, IntPtr completionHandler) {
            RunPanel(self, JavascriptDialogKind.Prompt, prompt, defaultText, completionHandler);
        }

        private static void RunPanel(IntPtr self, JavascriptDialogKind kind, IntPtr message, IntPtr defaultValue, IntPtr completionHandler) {
            var browser = Owner(self);
            if (browser == null) {
                CompleteDialog(kind, completionHandler, false, null);
                return;
            }

            var text = FromNSString(message);
            var defaultText = FromNSString(defaultValue);

            // the handler may be completed after this callback returns, so it has to be retained
            var handler = Block_copy(completionHandler);
            var handled = false;
            browser.Guard(() => {
                handled = browser.OnJavascriptDialog(kind, text, defaultText, (accepted, value) => {
                    browser.RunOnUIThread(() => {
                        CompleteDialog(kind, handler, accepted, value);
                        Block_release(handler);
                    });
                });
            });

            if (!handled) {
                // no one is interested in the dialog, dismiss it
                CompleteDialog(kind, handler, false, null);
                Block_release(handler);
            }
        }

        private static void CompleteDialog(JavascriptDialogKind kind, IntPtr handler, bool accepted, string value) {
            switch (kind) {
                case JavascriptDialogKind.Alert:
                    Block.Call(handler);
                    break;
                case JavascriptDialogKind.Confirm:
                    Block.Call(handler, (byte)(accepted ? 1 : 0));
                    break;
                default:
                    Block.Call(handler, accepted ? NSString(value ?? string.Empty) : IntPtr.Zero);
                    break;
            }
        }
    }
}
