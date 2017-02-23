using System;
using System.IO;
using CefSharp;

namespace WebViewControl {
    
    partial class WebView {

        private class CefDownloadHandler : IDownloadHandler {

            private readonly WebView OwnerWebView;
            private readonly Stream Stream;
            private readonly long TotalSize;
            private readonly string Filename;

            private long receivedSize;

            public CefDownloadHandler(WebView owner, Stream stream, string filename, long totalSize) {
                OwnerWebView = owner;
                Stream = stream;
                TotalSize = totalSize;
                Filename = filename;
            }

            void IDownloadHandler.OnBeforeDownload(IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
                // TODO JMN cef3
                /*string[] selectedFileNames;
                int selectedFilterIndex;
                try {
                    if (FileDialog.Show(OwnerWebView.ownerWindowHandle, FileDialog.Type.Save, "Save As...", "", downloadItem.SuggestedFileName, null, false, "", 0, out selectedFileNames, out selectedFilterIndex)) {
                        var stream = new FileStream(selectedFileNames[0], FileMode.Create);
                        handler = new DownloadHandler(OwnerWebView, stream, selectedFileNames[0], contentLength);
                        return true;
                    }
                } catch {
                    // this is not relevant. We don't want to throw an exception here (submit feedback would be thrown twice)
                }
                if (OwnerWebView.DownloadCanceled != null) {
                    OwnerWebView.ExecuteInUIThread(() => OwnerWebView.DownloadCanceled(filename));
                }
                return false;*/
            }

            void IDownloadHandler.OnDownloadUpdated(IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
                if (downloadItem.IsComplete) {
                    Stream.Flush();
                    Stream.Close();
                    if (OwnerWebView.DownloadCompleted != null) {
                        OwnerWebView.ExecuteInUIThread(() => OwnerWebView.DownloadCompleted(Filename));
                    }
                } else {
                    // TODO JMN cef3
                    // Stream.Write(data, 0, data.Length);
                    if (OwnerWebView.DownloadProgressChanged != null) {
                        OwnerWebView.ExecuteInUIThread(() => OwnerWebView.DownloadProgressChanged(Filename, downloadItem.ReceivedBytes, downloadItem.TotalBytes));
                    }
                }
            }
        }
    }
}
