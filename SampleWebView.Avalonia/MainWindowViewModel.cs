using System.ComponentModel;
using System.Reactive;
using ReactiveUI;
using WebViewControl;

namespace SampleWebView.Avalonia {
    class MainWindowViewModel : ReactiveObject {

        private string address;
        private string currentAddress;

        public MainWindowViewModel(WebView webview) {
            Address = CurrentAddress = "http://www.google.com/";

            NavigateCommand = ReactiveCommand.Create(() => {
                CurrentAddress = Address;
            });

            ShowDevToolsCommand = ReactiveCommand.Create(() => {
                webview.ShowDeveloperTools();
            });

            CutCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Cut();
            });

            CopyCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Copy();
            });

            PasteCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Paste();
            });

            UndoCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Undo();
            });

            RedoCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Redo();
            });

            SelectAllCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.SelectAll();
            });

            DeleteCommand = ReactiveCommand.Create(() => {
                webview.EditCommands.Delete();
            });
            
            BackCommand = ReactiveCommand.Create(() => {
                webview.GoBack();
            });
            
            ForwardCommand = ReactiveCommand.Create(() => {
                webview.GoForward();
            });

            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(CurrentAddress)) {
                Address = CurrentAddress;
            }
        }

        public string Address {
            get => address;
            set => this.RaiseAndSetIfChanged(ref address, value);
        }

        public string CurrentAddress {
            get => currentAddress;
            set => this.RaiseAndSetIfChanged(ref currentAddress, value);
        }

        public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowDevToolsCommand { get; }

        public ReactiveCommand<Unit, Unit> CutCommand { get; }

        public ReactiveCommand<Unit, Unit> CopyCommand { get; }

        public ReactiveCommand<Unit, Unit> PasteCommand { get; }

        public ReactiveCommand<Unit, Unit> UndoCommand { get; }

        public ReactiveCommand<Unit, Unit> RedoCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

        public ReactiveCommand<Unit, Unit> BackCommand { get; }
        
        public ReactiveCommand<Unit, Unit> ForwardCommand { get; }
    }
}
