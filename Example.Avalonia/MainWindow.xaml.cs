using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Example {

    internal class MainWindow : Window {

        private ExampleView browser;

        public MainWindow() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            var browserWrapper = this.FindControl<Decorator>("browserWrapper");

            browser = new ExampleView();
        }

        private void OnOpenDevToolsMenuItemClick(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            browser.ShowDeveloperTools();
        }
    }
}