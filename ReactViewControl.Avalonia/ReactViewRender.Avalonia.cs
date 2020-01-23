using Avalonia.Controls.Primitives;

namespace ReactViewControl {

    partial class ReactViewRender : TemplatedControl {

        partial void ExtraInitialize() {
            LogicalChildren.Add(WebView);
            VisualChildren.Add(WebView);
        }
    }
}
