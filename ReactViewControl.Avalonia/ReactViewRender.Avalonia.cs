using Avalonia.Controls;
using Avalonia.Styling;
using System;

namespace ReactViewControl {

    partial class ReactViewRender : ContentControl, IStyleable {

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            Content = WebView;
        }
    }
}
