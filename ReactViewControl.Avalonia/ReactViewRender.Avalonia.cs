using Avalonia.Controls.Primitives;

namespace ReactViewControl {

    partial class ReactViewRender : TemplatedControl {

        partial void ExtraInitialize() {
            VisualChildren.Add(WebView);
        }
    }
}
