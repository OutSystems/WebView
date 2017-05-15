using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        private class CefDownloadHandler : IDownloadHandler {

            private readonly WebView OwnerWebView;

            public CefDownloadHandler(WebView owner) {
                OwnerWebView = owner;
            }

            void IDownloadHandler.OnBeforeDownload(IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
                if (!callback.IsDisposed) {
                    using (callback) {
                        callback.Continue(downloadItem.SuggestedFileName, showDialog: true);
                    }
                }
            }

            void IDownloadHandler.OnDownloadUpdated(IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
                if (downloadItem.IsComplete) {
                    var downloadCompleted = OwnerWebView.DownloadCompleted;
                    if (downloadCompleted != null) {
                        OwnerWebView.ExecuteInUIThread(() => downloadCompleted(downloadItem.FullPath));
                    }
                } else if (downloadItem.IsCancelled) {
                    var downloadCancelled = OwnerWebView.DownloadCancelled;
                    if (downloadCancelled != null) {
                        OwnerWebView.ExecuteInUIThread(() => downloadCancelled(downloadItem.FullPath));
                    }
                } else {
                    var downloadProgressChanged = OwnerWebView.DownloadProgressChanged;
                    if (downloadProgressChanged != null) {
                        OwnerWebView.ExecuteInUIThread(() => downloadProgressChanged(downloadItem.FullPath, downloadItem.ReceivedBytes, downloadItem.TotalBytes));
                    }
                }
            }
        }
    }
}
