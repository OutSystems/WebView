using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalDownloadHandler : DownloadHandler {

            private WebView OwnerWebView { get; }

            public InternalDownloadHandler(WebView owner) {
                OwnerWebView = owner;
            }

            protected override void OnBeforeDownload(CefBrowser browser, CefDownloadItem downloadItem, string suggestedName, CefBeforeDownloadCallback callback) {
                callback.Continue(downloadItem.SuggestedFileName, showDialog: true);
            }

            protected override void OnDownloadUpdated(CefBrowser browser, CefDownloadItem downloadItem, CefDownloadItemCallback callback) {
                if (downloadItem.IsComplete) {
                    var downloadCompleted = OwnerWebView.DownloadCompleted;
                    if (downloadCompleted != null) {
                        OwnerWebView.AsyncExecuteInUI(() => downloadCompleted(downloadItem.FullPath));
                    }
                } else if (downloadItem.IsCanceled) {
                    var downloadCancelled = OwnerWebView.DownloadCancelled;
                    if (downloadCancelled != null) {
                        OwnerWebView.AsyncExecuteInUI(() => downloadCancelled(downloadItem.FullPath));
                    }
                } else {
                    var downloadProgressChanged = OwnerWebView.DownloadProgressChanged;
                    if (downloadProgressChanged != null) {
                        OwnerWebView.AsyncExecuteInUI(() => downloadProgressChanged(downloadItem.FullPath, downloadItem.ReceivedBytes, downloadItem.TotalBytes));
                    }
                }
            }
        }
    }
}
