using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tests
{

    internal class TestWindow : Window {

        public TestWindow() {
            AvaloniaXamlLoader.Load(this);
            var editor = this.FindControl<WebViewControl.WebView>("editor");
        }
    }
}
