using System;
using System.Diagnostics;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalLifeSpanHandler : LifeSpanHandler {

            private WebView OwnerWebView { get; }

            public InternalLifeSpanHandler(WebView webView) {
                OwnerWebView = webView;
            }

            protected override bool OnBeforePopup(CefBrowser browser, CefFrame frame, string targetUrl, string targetFrameName, CefWindowOpenDisposition targetDisposition, bool userGesture, CefPopupFeatures popupFeatures, CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings, ref CefDictionaryValue extraInfo, ref bool noJavascriptAccess) {     
                return OwnerWebView.HandleBeforePopup(targetUrl, frame.Url);
            }
        }
    }
}
