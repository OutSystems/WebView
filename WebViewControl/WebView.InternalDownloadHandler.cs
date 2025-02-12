using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
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
                var item = new DownloadItem(downloadItem);
                
                if (downloadItem.IsComplete) {
                    // DownloadCompleted
                    FireDownloadCompletedEvents(item);
                } else if (downloadItem.IsCanceled) {
                    // DownloadCancelled
                    FireDownloadCancelledEvents(item);
                } else if (downloadItem.IsInterrupted) {
                    // Interrupted
                    FireDownloadStoppedEvent(item);
                } else {
                    // ProgressChanged
                    FireDownloadPropertyChangedEvents(item);
                }
            }

            private void FireDownloadStartedEvents(DownloadItem downloadItem) {
                if (OwnerWebView.DownloadItemStarted != null) {
                    OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadItemStarted?.Invoke(downloadItem));
                }
            }

            private void FireDownloadPropertyChangedEvents(DownloadItem downloadItem) {
                if (OwnerWebView.DownloadProgressChanged != null) {
                    OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadProgressChanged?.Invoke(downloadItem.FullPath, downloadItem.ReceivedBytes, downloadItem.TotalBytes));
                }

                if (downloadItem.ReceivedBytes == 0) {
                    // DownloadStarted
                    //  We fire this here because CEF actually calls OnDownloadUpdated before OnBeforeDownload
                    FireDownloadStartedEvents(downloadItem);
                }

                if (OwnerWebView.DownloadItemProgressChanged != null) {
                    OwnerWebView.AsyncExecuteInUI(() =>
                        OwnerWebView.DownloadItemProgressChanged?.Invoke(downloadItem));
                }
            }

            private void FireDownloadCancelledEvents(DownloadItem downloadItem) {
                if (OwnerWebView.DownloadCancelled != null) {
                    OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadCancelled?.Invoke(downloadItem.FullPath));
                }
                FireDownloadStoppedEvent(downloadItem);
            }

            private void FireDownloadStoppedEvent(DownloadItem downloadItem) {
                if (OwnerWebView.DownloadItemStopped != null) {
                    OwnerWebView.AsyncExecuteInUI(() =>
                        OwnerWebView.DownloadItemStopped?.Invoke(downloadItem));
                }
            }

            private void FireDownloadCompletedEvents(DownloadItem downloadItem) {
                if (OwnerWebView.DownloadCompleted != null) {
                    OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadCompleted?.Invoke(downloadItem.FullPath));
                }

                if (OwnerWebView.DownloadItemCompleted != null) {
                    OwnerWebView.AsyncExecuteInUI(() => OwnerWebView.DownloadItemCompleted?.Invoke(downloadItem));
                }
            }
        }
    }

    public enum DownloadItemState {
        InProgress = 0, Complete = 1, Cancelled = 2, Interrupted = 3
    }

    public enum DownloadItemInterruptReason {
        None = 0,
        FileFailed = 1,
        FileAccessDenied = 2,
        FileNoSpace = 3,
        FileNameTooLong = 5,
        FileTooLarge = 6,
        FileVirusInfected = 7,
        FileTransientError = 10, // 0x0000000A
        FileBlocked = 11, // 0x0000000B
        FileSecurityCheckFailed = 12, // 0x0000000C
        FileTooShort = 13, // 0x0000000D
        FileHashMismatch = 14, // 0x0000000E
        FileSameAsSource = 15, // 0x0000000F
        NetworkFailed = 20, // 0x00000014
        NetworkTimeout = 21, // 0x00000015
        NetworkDisconnected = 22, // 0x00000016
        NetworkServerDown = 23, // 0x00000017
        NetworkInvalidRequest = 24, // 0x00000018
        ServerFailed = 30, // 0x0000001E
        ServerNoRange = 31, // 0x0000001F
        ServerBadContent = 33, // 0x00000021
        ServerUnauthorized = 34, // 0x00000022
        ServerCertProblem = 35, // 0x00000023
        ServerForbidden = 36, // 0x00000024
        ServerUnreachable = 37, // 0x00000025
        ServerContentLengthMismatch = 38, // 0x00000026
        ServerCrossOriginRedirect = 39, // 0x00000027
        UserCanceled = 40, // 0x00000028
        UserShutdown = 41, // 0x00000029
        Crash = 50, // 0x00000032
    }

    public sealed record DownloadItem {
        public DownloadItem(CefDownloadItem cefDownloadItem) {
            Id = cefDownloadItem.Id;
            TotalBytes = cefDownloadItem.TotalBytes;
            ReceivedBytes = cefDownloadItem.ReceivedBytes;
            CurrentSpeed = cefDownloadItem.CurrentSpeed;
            PercentComplete = cefDownloadItem.PercentComplete;
            Url = cefDownloadItem.Url;
            OriginalUrl = cefDownloadItem.OriginalUrl;
            FullPath = cefDownloadItem.FullPath ?? "";
            MimeType = cefDownloadItem.MimeType;
            InterruptReason = (DownloadItemInterruptReason)((int)cefDownloadItem.InterruptReason);

            if (CurrentSpeed > 0) {
                EstimatedTimeRemaining = RemainingBytes / CurrentSpeed;
            }

            if (cefDownloadItem.IsInProgress) {
                State = DownloadItemState.InProgress;
            } else if (cefDownloadItem.IsComplete) {
                State = DownloadItemState.Complete;
            } else if (cefDownloadItem.IsCanceled) {
                State = DownloadItemState.Cancelled;
            } else if (cefDownloadItem.IsInterrupted) {
                State = DownloadItemState.Interrupted;
            }
        }

        public uint Id { get; }
        public long TotalBytes { get; }
        public long ReceivedBytes { get; }
        public long CurrentSpeed { get; }
        public int PercentComplete { get; }

        public string Url { get; } = "";
        public string OriginalUrl { get; } = "";
        public string FullPath { get; } = "";
        public string MimeType { get; } = "";

        public string FileName => Path.GetFileName(FullPath);
        public string FileExtension => Path.GetExtension(FullPath);
        public long RemainingBytes => TotalBytes - ReceivedBytes;
        public long EstimatedTimeRemaining { get; }

        public DownloadItemInterruptReason InterruptReason { get; }
        public DownloadItemState State { get; }
    }
}
