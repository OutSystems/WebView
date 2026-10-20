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
                if (OwnerWebView.HasKeyPressedHandler && !browser.IsPopup) {
                    var handled = OwnerWebView.HandleKeyPressed(new KeyEventDetails(keyEvent.WindowsKeyCode, keyEvent.Character));
                    isKeyboardShortcut = false;
                    return handled;
                }
                return base.OnPreKeyEvent(browser, keyEvent, os_event, out isKeyboardShortcut);
            }
        }
    }
}
