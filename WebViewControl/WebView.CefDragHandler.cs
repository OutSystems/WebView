using CefSharp;
using CefSharp.Enums;
using System.Collections.Generic;
using System.Linq;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

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

                return false;
            }

            public void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions) {
                
            }
        }
    }
}
