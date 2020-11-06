using System.Reactive;
using ReactiveUI;

namespace SampleWebView.Avalonia {
    class MainWindowViewModel : ReactiveObject {

        private string address;
        private string currentAddress;

        public MainWindowViewModel() {
            Address = CurrentAddress = "http://www.google.com/";

            NavigateCommand = ReactiveCommand.Create(() => {
                CurrentAddress = Address;
            });
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
    }
}