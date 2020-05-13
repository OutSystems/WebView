using System;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ReactViewControl {

    partial class ReactView : ContentControl, IStyleable {

        Type IStyleable.StyleKey => typeof(ContentControl);

        partial void ExtraInitialize() {
            AttachedToVisualTree += OnAttachedToVisualTree;

            Content = View;

            // TODO needed ? FocusManager.SetIsFocusScope(this, true);
            // FocusManager.SetFocusedElement(this, View.FocusableElement);
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
