using Avalonia;
using Avalonia.Markup.Xaml;

namespace Example
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
