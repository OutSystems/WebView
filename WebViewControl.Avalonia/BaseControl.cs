using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace WebViewControl {
    public abstract class BaseControl : Control {
        protected abstract void InternalDispose();

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e) {
            base.OnDetachedFromLogicalTree(e);

            if (e.Root is Window w && w.PlatformImpl is null) {
                // Window was closed.
                InternalDispose();
            }
        }
    }
}
