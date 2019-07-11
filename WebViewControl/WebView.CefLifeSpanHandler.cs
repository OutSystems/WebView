using System;
using System.Diagnostics;
using Xilium.CefGlue;

namespace WebViewControl {

    partial class WebView
    {

        private class CefLifeSpanHandler : Xilium.CefGlue.Common.Handlers.LifeSpanHandler
        {

            private WebView OwnerWebView { get; }

            public CefLifeSpanHandler(WebView webView) {
                OwnerWebView = webView;
            }

            public event Action</*url*/string> PopupOpening;

            protected override bool OnBeforePopup(CefBrowser browser, CefFrame frame, string targetUrl, string targetFrameName, CefWindowOpenDisposition targetDisposition, bool userGesture, CefPopupFeatures popupFeatures, CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings, ref bool noJavascriptAccess) {
                if (targetUrl.StartsWith(ChromeInternalProtocol, StringComparison.InvariantCultureIgnoreCase)) {
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
                    if (PopupOpening != null) {
                        PopupOpening(targetUrl);
                    } else {
                        // if we are opening a popup then this should go to the default browser
                        Process.Start(targetUrl);
                    }
                } catch {
                    // Try this method for machines which are not properly configured
                    try {
                        Process.Start("explorer.exe", "\"" + targetUrl + "\"");
                    } catch {
                        // if we can't handle the command line let's continue the normal request with the popup
                        // with this, will not blow in the users face
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
