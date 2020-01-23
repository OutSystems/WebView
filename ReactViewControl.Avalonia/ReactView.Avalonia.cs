using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using System;

namespace ReactViewControl {

    partial class ReactView : ContentControl, IStyleable {

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            AttachedToLogicalTree += OnAttachedToLogicalTree;

            Content = View;

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
