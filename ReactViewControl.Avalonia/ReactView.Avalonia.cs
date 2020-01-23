using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System;

namespace ReactViewControl {

    partial class ReactView : TemplatedControl {

        partial void ExtraInitialize() {
            AttachedToLogicalTree += OnAttachedToLogicalTree;

            LogicalChildren.Add(View);
            VisualChildren.Add(View);

            // TODO needed ? FocusManager.SetIsFocusScope(this, true);
            // FocusManager.SetFocusedElement(this, View.FocusableElement);
        }

        private void OnAttachedToLogicalTree(object sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e) {
            AttachedToLogicalTree -= OnAttachedToLogicalTree;
            TryLoadComponent();
        }

        private static void AsyncExecuteInUI(Action action, bool lowPriority) {
            Dispatcher.UIThread.Post(action, lowPriority ? DispatcherPriority.Background : DispatcherPriority.Normal);
        }
    }
}
