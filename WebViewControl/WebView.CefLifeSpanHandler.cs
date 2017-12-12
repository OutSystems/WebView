using System;
using System.Diagnostics;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        private class CefLifeSpanHandler : ILifeSpanHandler {

            private readonly WebView OwnerWebView;

            public CefLifeSpanHandler(WebView webView) {
                OwnerWebView = webView;
            }

            void ILifeSpanHandler.OnBeforeClose(IWebBrowser browserControl, IBrowser browser) { }
            
            bool ILifeSpanHandler.OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser) {
                newBrowser = null;

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

                // if we are opening a popup then this should go to the default browser
                try {
                    Process.Start(targetUrl);
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

            void ILifeSpanHandler.OnAfterCreated(IWebBrowser browserControl, IBrowser browser) {
            }

            bool ILifeSpanHandler.DoClose(IWebBrowser browserControl, IBrowser browser) {
                return false;
            }
        }
    }
}
