using System;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {
        
        private class InternalKeyboardHandler : KeyboardHandler {

            private WebView OwnerWebView { get; }

            public InternalKeyboardHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnPreKeyEvent(CefBrowser browser, CefKeyEvent keyEvent, IntPtr os_event, out bool isKeyboardShortcut) {
                var handler = OwnerWebView.KeyPressed;
                if (handler != null && !browser.IsPopup) {
                    handler(keyEvent, out var handled);
                    isKeyboardShortcut = false;
                    return handled;
                }
                return base.OnPreKeyEvent(browser, keyEvent, os_event, out isKeyboardShortcut);
            }
        }
    }
}
