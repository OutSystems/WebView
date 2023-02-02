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
                    if (OwnerWebView.DownloadCompleted != null) {
                        OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadCompleted?.Invoke(downloadItem.FullPath));
                    }
                } else if (downloadItem.IsCanceled) {
                    if (OwnerWebView.DownloadCancelled != null) {
                        OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadCancelled?.Invoke(downloadItem.FullPath));
                    }
                } else {
                    if (OwnerWebView.DownloadProgressChanged != null) {
                        OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadProgressChanged?.Invoke(downloadItem.FullPath, downloadItem.ReceivedBytes, downloadItem.TotalBytes));
                    }
                }
            }
        }
    }
}
