using System;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl;

partial class WebView {
    private class InternalKeyboardHandler : KeyboardHandler {
        public InternalKeyboardHandler(WebView webView) {
            OwnerWebView = webView;
        }

        private WebView OwnerWebView { get; }

        protected override bool OnPreKeyEvent(CefBrowser browser, CefKeyEvent keyEvent, IntPtr os_event,
            out bool isKeyboardShortcut) {
            KeyPressedEventHandler handler = OwnerWebView.KeyPressed;
            if (handler != null && !browser.IsPopup) {
                handler(keyEvent, out bool handled);
                isKeyboardShortcut = false;
                return handled;
            }

            if (OwnerWebView.AllowDeveloperTools && keyEvent.WindowsKeyCode == (int)KnownWindowsKeyCodes.F12) {
                // F12 Pressed
                OwnerWebView.ToggleDeveloperTools();
                isKeyboardShortcut = true;
                return true;
            }

            return base.OnPreKeyEvent(browser, keyEvent, os_event, out isKeyboardShortcut);
        }
    }

    private enum KnownWindowsKeyCodes {
        F12 = 123
    }
}