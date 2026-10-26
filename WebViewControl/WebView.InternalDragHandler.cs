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
                    var filePaths = dragData.GetFilePaths();
                    if (filePaths != null) {
                        filesDragging(filePaths);
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

            protected override void OnDraggableRegionsChanged(CefBrowser browser, CefFrame frame, CefDraggableRegion[] regions) {

            }
        }
    }
}
