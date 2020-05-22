using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace ReactViewControl {

    partial class ReactView : Control {

        partial void ExtraInitialize() {
            AttachedToVisualTree += OnAttachedToVisualTree;
            VisualChildren.Add(View);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) {
            e.Handled = true;
            View.Focus();
        }

        private void OnAttachedToVisualTree(object sender, Avalonia.VisualTreeAttachmentEventArgs e) {
            AttachedToVisualTree -= OnAttachedToVisualTree;
            TryLoadComponent();
        }

        private static void AsyncExecuteInUI(Action action, bool lowPriority) {
            Dispatcher.UIThread.Post(action, lowPriority ? DispatcherPriority.Background : DispatcherPriority.Normal);
        }
    }
}
