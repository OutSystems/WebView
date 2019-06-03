using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        private class CefDownloadHandler : IDownloadHandler {

            private WebView OwnerWebView { get; }

            public CefDownloadHandler(WebView owner) {
                OwnerWebView = owner;
            }

            void IDownloadHandler.OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
                if (!callback.IsDisposed) {
                    using (callback) {
                        callback.Continue(downloadItem.SuggestedFileName, showDialog: true);
                    }
                }
            }

            void IDownloadHandler.OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
                if (downloadItem.IsComplete) {
                    var downloadCompleted = OwnerWebView.DownloadCompleted;
                    if (downloadCompleted != null) {
                        OwnerWebView.AsyncExecuteInUI(() => downloadCompleted(downloadItem.FullPath));
                    }
                } else if (downloadItem.IsCancelled) {
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
