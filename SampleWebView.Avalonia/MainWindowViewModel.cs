using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WebViewControl;

namespace SampleWebView.Avalonia;

public class MainWindowViewModel : ReactiveObject {
    #region Fields

    private readonly Dictionary<uint, DownloadItem> downloads = new();
    private readonly List<DownloadItem> completedDownloads = new();
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
    private readonly ReactiveCommand<Unit, Unit> exitCommand;

    private string address;
    private string currentAddress;
    
    private double downloadPercentage;
    private bool isDownloading;
    private bool isDownloadDeterminate;
    private string downloadMessage;
    private string downloadProgress;

    private string source;
    private bool sourceAvailable;
    private string text;
    private bool textAvailable;

    #endregion Fields

    public MainWindowViewModel(WebView webview) {
        Address = CurrentAddress = "http://www.google.com/";
        //Address = CurrentAddress = "http://www.testfile.org/";
        webView = webview;

        webview.AllowDeveloperTools = true;

        webview.Navigated += OnNavigated;

        webView.PopupOpening += OnPopupOpening;

        webview.DownloadItemStarted += DownloadItemStarted;
        webview.DownloadItemProgressChanged += DownloadItemProgressChanged;
        webView.DownloadItemCompleted += DownloadItemCompleted;
        webview.DownloadItemStopped += DownloadItemStopped;

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

        exitCommand = ReactiveCommand.Create(() => {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp) {
                desktopApp.Shutdown();
            }
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

    public ReactiveCommand<Unit, Unit> ExitCommand => exitCommand;

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

    private void OnPopupOpening(string url) {
        // Catch window.open(), since we are a SDI browser we will simply Navigate to the new url 
        CurrentAddress = url;
    }

    private void OnNavigated(string url, string frameName) {
        if (!string.IsNullOrWhiteSpace(frameName)) {
            return;
        }

        Text = "";
        TextAvailable = false;
        //webView.GetText(OnTextAvailable);

        Source = "";
        SourceAvailable = false;
    }

    private void OnSourceAvailable(string str) {
        Source = str;
        SourceAvailable = true;

        var sourceFilePath = SaveAs("Data\\sample.html", str);

        Debug.WriteLine($"Page source saved successfully to {sourceFilePath}");
    }

    private void OnTextAvailable(string str) {
        Text = str;
        TextAvailable = true;

        var textFilePath = SaveAs("Data\\sample.txt", str);

        Debug.WriteLine($"Page text saved successfully to {textFilePath}");
    }

    private string SaveAs(string fileName, string fileContents) {
        if (string.IsNullOrWhiteSpace(fileName) || fileName == "/" || fileName == "\\") {
            return "";
        }

        try {
            var fileInfo = new FileInfo(fileName);

            if (fileInfo.Directory is null || fileInfo.Attributes == FileAttributes.Directory) {
                return "";
            }

            if (!fileInfo.Directory.Exists) {
                fileInfo.Directory.Create();
            }

            if (fileInfo.Exists && fileInfo.IsReadOnly) {
                return "";
            }

            using var sourceStreamWriter = File.CreateText(fileName);
            sourceStreamWriter.Write(fileContents);
            sourceStreamWriter.Close();

            return fileInfo.FullName;
        } catch {
            return "";
        }
    }

    #region Download V2 Event Implementation

    private void DownloadItemStarted(DownloadItem item) {
        downloads.Add(item.Id, item);

        IsDownloading = true;
    }

    private void DownloadItemProgressChanged(DownloadItem item) {
        downloads[item.Id] = item;

        UpdateDownloadPanel();
    }

    private void DownloadItemCompleted(DownloadItem item) {
        downloads.Remove(item.Id);
        completedDownloads.Add(item);

        // Manage UI
        IsDownloadDeterminate = true;
        DownloadPercentage = 100.0;
        DownloadMessage = $"Download Completed: {item.FullPath}";
        DownloadProgress = $"{item.ReceivedBytes}/{item.TotalBytes}, Time Remaining: None";

        CloseDownloadPanel();
    }

    private void DownloadItemStopped(DownloadItem item) {
        downloads.Remove(item.Id);

        // Manage UI
        IsDownloadDeterminate = false;
        DownloadProgress = "";
        DownloadMessage = $"Download Stopped ({item.InterruptReason}): {item.FullPath}";

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

    #endregion Download V2 Event Implementation
}