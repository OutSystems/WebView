using System;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Xilium.CefGlue.Common;

namespace WebViewControl {

    partial class WebView : BaseControl {

        private static bool osrEnabled = true;

        private bool IsInDesignMode => false;

        public static bool OsrEnabled {
            get => osrEnabled;
            set {
                if (CefRuntimeLoader.IsLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(OsrEnabled)} after WebView engine has been loaded");
                }
                osrEnabled = value;
            }
        }

        public static readonly StyledProperty<string> AddressProperty =
            AvaloniaProperty.Register<WebView, string>(nameof(Address), defaultBindingMode: BindingMode.TwoWay);

        public string Address {
            get => GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change) {
            base.OnPropertyChanged(change);

            if (change.Property == AddressProperty) {
                if (InternalAddress != Address) {
                    InternalAddress = Address;
                }
            }
        }

        partial void ExtraInitialize() {
            VisualChildren.Add(chromium);
            chromium.AddressChanged += (o, address) => ExecuteInUI(() => Address = address);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                ToggleDeveloperTools();
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(GotFocusEventArgs e) {
            e.Handled = true;
            chromium.Focus();
        }

        protected override void InternalDispose() => Dispose();

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

        internal void InitializeBrowser(WindowBase hostingWindow, int initialWidth, int initialHeight) {
            chromium.CreateBrowser(hostingWindow, initialWidth, initialHeight);
        }
    }
}
