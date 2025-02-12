using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using WebViewControl;

namespace SampleWebView.Avalonia;

public class MainWindowV1ViewModel : ReactiveObject {
    #region Fields

    private readonly Dictionary<string, DownloadItemModel> downloads = new();
    private readonly WebView webView;

    private readonly ReactiveCommand<Unit, Unit> navigateCommand;
    private readonly ReactiveCommand<Unit, Unit> showDevToolsCommand;
    private readonly ReactiveCommand<Unit, Unit> cutCommand;
    private readonly ReactiveCommand<Unit, Unit> copyCommand;
    private readonly ReactiveCommand<Unit, Unit> pasteCommand;
    private readonly ReactiveCommand<Unit, Unit> undoCommand;
    private readonly ReactiveCommand<Unit, Unit> redoCommand;
    private readonly ReactiveCommand<Unit, Unit> selectAllCommand;
    private readonly ReactiveCommand<Unit, Unit> deleteCommand;
    private readonly ReactiveCommand<Unit, Unit> backCommand;
    private readonly ReactiveCommand<Unit, Unit> forwardCommand;
    private readonly ReactiveCommand<Unit, Unit> getSourceCommand;
    private readonly ReactiveCommand<Unit, Unit> getTextCommand;

    private string address;
    private string currentAddress;
    
    private bool isDownloading;
    private bool isDownloadDeterminate;
    private double downloadPercentage;
    private string downloadMessage;
    private string downloadProgress;

    private string source;
    private bool sourceAvailable;
    private string text;
    private bool textAvailable;

    #endregion Fields

    public MainWindowV1ViewModel(WebView webview) {
        Address = CurrentAddress = "http://www.google.com/";
        //Address = CurrentAddress = "http://www.testfile.org/";
        webView = webview;
        
        webview.AllowDeveloperTools = true;

        webview.Navigated += OnNavigated;

        webview.DownloadCancelled += OnDownloadCancelled;
        webview.DownloadCompleted += OnDownloadCompleted;
        webview.DownloadProgressChanged += OnDownloadProgressChanged;

        navigateCommand = ReactiveCommand.Create(() => {
            CurrentAddress = Address;
        });

        showDevToolsCommand = ReactiveCommand.Create(webview.ShowDeveloperTools);

        cutCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Cut();
        });

        copyCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Copy();
        });

        pasteCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Paste();
        });

        undoCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Undo();
        });

        redoCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Redo();
        });

        selectAllCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.SelectAll();
        });

        deleteCommand = ReactiveCommand.Create(() => {
            webview.EditCommands.Delete();
        });

        backCommand = ReactiveCommand.Create(webview.GoBack);

        forwardCommand = ReactiveCommand.Create(webview.GoForward);

        getTextCommand = ReactiveCommand.Create(() => {
            webview.GetText(OnTextAvailable);
        });

        getSourceCommand = ReactiveCommand.Create(() => {
            webview.GetSource(OnSourceAvailable);
        });

        PropertyChanged += OnPropertyChanged;


    }

    public ReactiveCommand<Unit, Unit> NavigateCommand => navigateCommand;

    public ReactiveCommand<Unit, Unit> ShowDevToolsCommand => showDevToolsCommand;

    public ReactiveCommand<Unit, Unit> CutCommand => cutCommand;

    public ReactiveCommand<Unit, Unit> CopyCommand => copyCommand;

    public ReactiveCommand<Unit, Unit> PasteCommand => pasteCommand;

    public ReactiveCommand<Unit, Unit> UndoCommand => undoCommand;

    public ReactiveCommand<Unit, Unit> RedoCommand => redoCommand;

    public ReactiveCommand<Unit, Unit> SelectAllCommand => selectAllCommand;

    public ReactiveCommand<Unit, Unit> DeleteCommand => deleteCommand;

    public ReactiveCommand<Unit, Unit> BackCommand => backCommand;

    public ReactiveCommand<Unit, Unit> ForwardCommand => forwardCommand;

    public ReactiveCommand<Unit, Unit> GetSourceCommand => getSourceCommand;

    public ReactiveCommand<Unit, Unit> GetTextCommand => getTextCommand;

    public string Address {
        get => address;
        set => this.RaiseAndSetIfChanged(ref address, value);
    }

    public string CurrentAddress {
        get => currentAddress;
        set => this.RaiseAndSetIfChanged(ref currentAddress, value);
    }

    public bool IsDownloading {
        get => isDownloading;
        set => this.RaiseAndSetIfChanged(ref isDownloading, value);
    }

    public bool IsDownloadDeterminate {
        get => isDownloadDeterminate;
        set => this.RaiseAndSetIfChanged(ref isDownloadDeterminate, value);
    }

    public double DownloadPercentage {
        get => downloadPercentage;
        set => this.RaiseAndSetIfChanged(ref downloadPercentage, value);
    }

    public string DownloadMessage {
        get => downloadMessage;
        set => this.RaiseAndSetIfChanged(ref downloadMessage, value);
    }

    public string DownloadProgress {
        get => downloadProgress;
        set => this.RaiseAndSetIfChanged(ref downloadProgress, value);
    }

    public string Source {
        get => source;
        set => this.RaiseAndSetIfChanged(ref source, value);
    }

    public bool SourceAvailable {
        get => sourceAvailable;
        set => this.RaiseAndSetIfChanged(ref sourceAvailable, value);
    }

    public string Text {
        get => text;
        set => this.RaiseAndSetIfChanged(ref text, value);
    }

    public bool TextAvailable {
        get => textAvailable;
        set => this.RaiseAndSetIfChanged(ref textAvailable, value);
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(CurrentAddress)) {
            Address = CurrentAddress;
        }
    }

    private void OnNavigated(string url, string frameName) {
        if (!string.IsNullOrWhiteSpace(frameName)) {
            return;
        }

        Text = "";
        TextAvailable = false;
        webView.GetText(OnTextAvailable);

        Source = "";
        SourceAvailable = false;
    }

    private void OnSourceAvailable(string str) {
        Source = str;
        SourceAvailable = true;
    }

    private void OnTextAvailable(string str) {
        Text = str;
        TextAvailable = true;
    }

    #region Download V1 Event Implementation

    private void OnDownloadProgressChanged(string resourcePath, long receivedBytes, long totalBytes) {
        // Tracking multiple file downloads at once is potentially wonky due to not being given the downloadItem.Id

        // Download Started
        if (receivedBytes == 0) {
            //  Since the File Dialog is Modal we know that there can only ever be one downloadItem = "" at a time
            downloads.Add("", new DownloadItemModel("", receivedBytes, totalBytes));
        }

        // Progress Changed
        DownloadItemModel downloadItem;//= new DownloadItem(resourcePath, receivedBytes, totalBytes);
        // The download proceeds in the background while waiting for the resourcePath from the user
        if (!string.IsNullOrWhiteSpace(resourcePath)) {
            // Try to get the downloadItem by resourcePath
            if (!downloads.TryGetValue(resourcePath, out downloadItem)) {
                // Not found, so get the downloadItem = ""
                if (downloads.TryGetValue("", out downloadItem)) {
                    // Now we need to first remove it from the collection as "", then put it back in the collection as resourcePath
                    downloads.Remove("");
                    downloadItem.FullPath = resourcePath;
                    downloads.Add(resourcePath, downloadItem);
                }
            }
        } else {
            // Get the downloadItem = ""
            downloads.TryGetValue("", out downloadItem);
        }

        // Now update the downloadItem...
        downloadItem?.Update(resourcePath, receivedBytes, totalBytes);

        UpdateDownloadPanel();

        // Download Completed
        if (receivedBytes == totalBytes && !string.IsNullOrWhiteSpace(resourcePath)) {
            // We have to stop tracking here because the resourcePath could change between now and the DownloadCompleted firing
            //  the PropertyChanged Event will fire two more time after this???, so rather than remove it now we flag it for removal by the Completed Event
            downloadItem?.SetCompleted();
        }

        //Debug.WriteLine($"{nameof(OnDownloadProgressChanged)}( count: {downloads.Count}, downloadItem: ( fullPath: {downloadItem.FullPath}, receivedBytes: {downloadItem.ReceivedBytes}, totalBytes {downloadItem.TotalBytes}, percentage {downloadItem.PercentComplete}, isCompleted {downloadItem.IsCompleted} ))");
    }

    private void OnDownloadCompleted(string resourcePath) {
        // Here the resourcePath may be different from the resourcePath passed to PropertyChanged

        // Remove the Completed DownloadItems
        var completed = downloads.Values
            .Where(x => x.IsCompleted)
            .ToList();

        if (completed.Count == 1) {
            var downloadItem = completed[0];
            downloadItem.Update(resourcePath);
            downloads.Remove(downloadItem.FullPath);
            completed.Add(downloadItem);
        }
        foreach (var item in completed) {
            downloads.Remove(item.FullPath);
        }

        DownloadMessage = $"Download Completed: {Path.GetFileName(resourcePath)}";
        DownloadPercentage = 100.0;
        IsDownloadDeterminate = true;

        CloseDownloadPanel();
    }

    private void OnDownloadCancelled(string resourcePath) {
        // Here the resourcePath probably will be null
        DownloadItemModel downloadItem; 
        if (string.IsNullOrWhiteSpace(resourcePath)) {
            downloads.Remove("", out downloadItem);
        } else {
            downloads.Remove(resourcePath, out downloadItem);
        }

        downloadItem?.SetCancelled(resourcePath);

        DownloadMessage = "Download Cancelled";
        IsDownloadDeterminate = false;

        CloseDownloadPanel();
    }

    private void CloseDownloadPanel() {
        Debug.WriteLine($"{nameof(CloseDownloadPanel)}: ({downloads.Count})");
        if (downloads.Count != 0) {
            return;
        }

        Task.Delay(2000)
            .ContinueWith(t => {
                if (downloads.Count != 0) {
                    return;
                }

                IsDownloading = false;
            });
    }

    private void UpdateDownloadPanel() {
        var downloadItem = downloads.Values
            .OrderByDescending(x => x.PercentComplete)
            .First();

        IsDownloading = true;
        IsDownloadDeterminate = true;
        DownloadPercentage = downloadItem.PercentComplete;
        DownloadMessage = $"Downloading: {downloadItem.FullPath}";

        var estimated = downloadItem.CurrentSpeed == 0 ?
            "Unknown" : downloadItem.RemainingBytes == 0 ?
                "None" :
                $"{downloadItem.EstimatedTimeRemaining} sec.";

        DownloadProgress = $"{downloadItem.ReceivedBytes}/{downloadItem.TotalBytes} bytes, Time Remaining: {estimated}";
    }

    private class DownloadItemModel(string fullPath, long receivedBytes, long totalBytes) {
        private readonly Stopwatch elapsedTime = Stopwatch.StartNew();

        public string FullPath { get; set; } = fullPath;

        public string FileName {
            get {
                return Path.GetFileName(FullPath);
            }
        }

        public long ReceivedBytes { get; set; } = receivedBytes;

        public long TotalBytes { get; set; } = totalBytes;

        public double PercentComplete {
            get {
                return (double)ReceivedBytes / TotalBytes * 100.0;
            }
        }

        public long RemainingBytes {
            get {
                return TotalBytes - ReceivedBytes;
            }
        }

        public long CurrentSpeed {
            get {
                return (ReceivedBytes / elapsedTime.Elapsed.Seconds);
            }
        }   // BytesPerSecond

        public long EstimatedTimeRemaining {
            get;
            set;
        }   // Seconds

        public string StoppedReason { get; set; }

        public bool IsCompleted { get; set; }

        public void Update(string fullPath, long receivedBytes, long totalBytes) {
            FullPath = fullPath;
            ReceivedBytes = receivedBytes;
            TotalBytes = totalBytes;

            if (RemainingBytes == 0) {
                elapsedTime.Stop();
            }

            if (CurrentSpeed > 0) {
                EstimatedTimeRemaining = (long)((double)RemainingBytes / CurrentSpeed);
            }
        }

        public void Update(string fullPath) {
            FullPath = fullPath;
        }

        public void SetCompleted() {
            elapsedTime.Stop();
            IsCompleted = true;
        }

        public void SetCancelled(string fullPath) {
            elapsedTime.Stop();
            FullPath = fullPath;
            StoppedReason = "UserCancelled";
            ReceivedBytes = 0;
        }
    }

    #endregion Download V1 Event Implementation
}