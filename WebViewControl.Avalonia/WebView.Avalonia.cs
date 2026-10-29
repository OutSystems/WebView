using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

namespace WebViewControl {

    partial class WebView {

        public static readonly StyledProperty<string> AddressProperty =
            AvaloniaProperty.Register<WebView, string>(nameof(Address), defaultBindingMode: BindingMode.TwoWay);

        public string Address {
            get => GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == AddressProperty) {
                InternalAddress = Address;
            }
        }

        partial void ExtraInitialize() {
            VisualChildren.Add(host);
            host[!FocusableProperty] = this[!FocusableProperty];
        }

        protected override void OnGotFocus(GotFocusEventArgs e) {
            if (!e.Handled) {
                e.Handled = true;
                base.OnGotFocus(e);

                // use async call to avoid reentrancy, otherwise the webview will fight to get the focus
                Dispatcher.UIThread.Post(() => {
                    if (IsFocused && !isDisposing && browser.IsCreated && !OnSetFocus(isSystemEvent: true)) {
                        browser.Focus();
                        OnGotFocus();
                    }
                }, DispatcherPriority.Background);
            }
        }

        protected override void InternalDispose() => Dispose();

        /// <summary>
        /// Runs the action in the UI thread: immediately when already in it, or asynchronously otherwise.
        /// </summary>
        private static void RunInUI(Action action) {
            if (Dispatcher.UIThread.CheckAccess()) {
                action();
            } else {
                Dispatcher.UIThread.Post(action);
            }
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

        internal void InitializeBrowser(int initialWidth, int initialHeight) {
            EnsureBrowserCreated(initialWidth, initialHeight);
        }

        /// <summary>
        /// Called when the webview is requesting focus. Return false to allow the
        /// focus to be set or true to cancel setting the focus.
        /// <paramref name="isSystemEvent">True if is a system focus event, or false if is a navigation</paramref>
        /// </summary>
        protected virtual bool OnSetFocus(bool isSystemEvent) {
            // VisualRoot can be null when webview is not yet added to the Visual tree
            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager.GetFocusedElement();
            return !(focusedElement == host || focusedElement == this);
        }
    }
}
