using CefSharp;
using CefSharp.Enums;
using System.Collections.Generic;
using System.Linq;

namespace WebViewControl {
    partial class WebView {
        private class CefDragHandler : IDragHandler {

            private WebView OwnerWebView { get; }

            public CefDragHandler(WebView owner) {
                OwnerWebView = owner;
            }

            public bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser, IDragData dragData, DragOperationsMask mask) {
                var filesDragging = OwnerWebView.FilesDragging;
                if (filesDragging != null) {
                    var fileNames = dragData.FileNames;
                    if (fileNames != null) {
                        filesDragging(fileNames.ToArray());
                    }
                }

                var textDragging = OwnerWebView.TextDragging;
                if (textDragging != null) {
                    var textContent = dragData.FragmentText;
                    if (!string.IsNullOrEmpty(textContent)) {
                        textDragging(textContent);
                    }
                }

                return false;
            }

            public void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions) {

            }
        }
    }
}
