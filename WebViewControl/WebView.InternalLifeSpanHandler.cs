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
               if (UrlHelper.IsChromeInternalUrl(targetUrl)) {
                    return false;
                }

                if (Uri.IsWellFormedUriString(targetUrl, UriKind.RelativeOrAbsolute)) {
                    var uri = new Uri(targetUrl);
                    if (!uri.IsAbsoluteUri) {
                        // turning relative urls into full path to avoid that someone runs custom command lines
                        targetUrl = new Uri(new Uri(frame.Url), uri).AbsoluteUri;
                    }
                } else {
                    return false; // if the url is not well formed let's use the browser to handle the things
                }

                try {
                    var popupOpening = OwnerWebView.PopupOpening;
                    if (popupOpening != null) {
                        popupOpening(targetUrl);
                    } else {
                        UrlHelper.OpenInExternalBrowser(targetUrl);
                    }
                } catch {
                    // if we can't handle the command line let's continue the normal request with the popup
                    // with this, will not blow in the users face
                    return false;
                }

                return true;
            }
        }
    }
}
