using System;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace WebViewControl {

    partial class WebView : TemplatedControl {

        partial void ExtraInitialize() {
            VisualChildren.Add(chromium);

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;

            // TODO needed ? FocusManager.SetIsFocusScope(this, true);
            // FocusManager.SetFocusedElement(this, FocusableElement);
        }

        internal IInputElement FocusableElement => chromium;

        private bool IsInDesignMode => false;

        protected override void OnKeyDown(KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                ToggleDeveloperTools();
                e.Handled = true;
            }
        }

        private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e) {
            if (e.Root is Window window) {
                window.Closed += OnHostWindowClosed;
            }
        }

        private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e) {
            if (e.Root is Window window) {
                window.Closed -= OnHostWindowClosed;
            }
        }
        private void OnHostWindowClosed(object sender, EventArgs e) {
            ((Window)sender).Closed -= OnHostWindowClosed;
            Dispose();
        }

        private void ForwardException(ExceptionDispatchInfo exceptionInfo) {
            // TODO
        }

        private T ExecuteInUI<T>(Func<T> action) {
            if (Dispatcher.UIThread.CheckAccess()) {
                return action();
            }
            return Dispatcher.UIThread.InvokeAsync<T>(action).Result;
        }

        private void AsyncExecuteInUI(Action action) {
            if (isDisposing) {
                return;
            }
            // use async call to avoid dead-locks, otherwise if the source action tries to to evaluate js it would block
            Dispatcher.UIThread.InvokeAsync(
                () => {
                    if (!isDisposing) {
                        ExecuteWithAsyncErrorHandling(action);
                    }
                },
                DispatcherPriority.Normal);
        }

        private static bool IsFrameworkAssemblyName(string name) {
            return name.StartsWith("Avalonia") || name == "mscorlib";
        }
    }
}
