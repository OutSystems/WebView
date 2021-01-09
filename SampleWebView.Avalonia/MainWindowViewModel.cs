using System.ComponentModel;
using System.Reactive;
using ReactiveUI;
using WebViewControl;

namespace SampleWebView.Avalonia {
    class MainWindowViewModel : ReactiveObject {

        private string address;
        private string currentAddress;

        public MainWindowViewModel(WebView webview) {
            Address = CurrentAddress = "embedded://webview/SampleWebView.Avalonia/index.html";

            NavigateCommand = ReactiveCommand.Create(() => {
                CurrentAddress = Address;
            });

            ShowDevToolsCommand = ReactiveCommand.Create(() => {
                webview.ShowDeveloperTools();
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
    }
}