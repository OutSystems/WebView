using System.Reactive;
using ReactiveUI;

namespace SampleWebView.Avalonia
{
    class MainWindowViewModel : ReactiveObject
    {
        private string _address;
        private string _currentAddress;

        public MainWindowViewModel()
        {
            Address = CurrentAddress = "http://www.google.co.uk/";
            
            NavigateCommand = ReactiveCommand.Create(() =>
            {
                CurrentAddress = Address;
            });
        }

        public string Address
        {
            get => _address;
            set => this.RaiseAndSetIfChanged(ref _address, value);
        }

        public string CurrentAddress
        {
            get => _currentAddress;
            set => this.RaiseAndSetIfChanged(ref _currentAddress, value);
        }
        
        public ReactiveCommand<Unit, Unit> NavigateCommand { get; }
    }
}