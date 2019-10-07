using Avalonia;
using Avalonia.Markup.Xaml;

namespace Example.Avalonia {
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
