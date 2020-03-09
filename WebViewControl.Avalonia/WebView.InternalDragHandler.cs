using System;
using System.Collections.Generic;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {
    partial class WebView {
        private class InternalDragHandler : DragHandler {

            private WebView OwnerWebView { get; }

            public InternalDragHandler(WebView owner) {
                OwnerWebView = owner;
            }

            protected override bool OnDragEnter(CefBrowser browser, CefDragData dragData, CefDragOperationsMask mask) {
                var filesDragging = OwnerWebView.FilesDragging;
                if (filesDragging != null) {
                    var fileNames = dragData.GetFileNames();
                    if (fileNames != null) {
                        filesDragging(fileNames);
                    }
                }

                return false;
            }

            protected override void OnDraggableRegionsChanged(CefBrowser browser, CefFrame frame, CefDraggableRegion[] regions) {

            }
        }
    }
}
