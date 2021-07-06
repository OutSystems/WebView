using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WebViewControl {

    partial class WebView : UserControl {

        internal IInputElement FocusableElement => chromium;

        private bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(this);

        public static readonly DependencyProperty AddressProperty = DependencyProperty.Register(nameof(Address), typeof(string), typeof(WebView));

        public string Address {
            get { return (string)GetValue(AddressProperty); }
            set { SetValue(AddressProperty, value); }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            // IWindowService is a WPF internal property set when component is loaded into a new window, even if the window isn't shown
            switch (e.Property.Name) {
                case "IWindowService":
                    if (e.OldValue is Window oldWindow) {
                        oldWindow.Closed -= OnHostWindowClosed;
                    }

                    if (e.NewValue is Window newWindow) {
                        newWindow.Closed += OnHostWindowClosed;
                    }
                    break;

                case nameof(Address):
                    InternalAddress = (string)e.NewValue;
                    break;
            }
        }

        partial void ExtraInitialize() {
            Content = chromium;

            chromium.AddressChanged += (o, address) => AsyncExecuteInUI(() => Address = address);

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, FocusableElement);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                ToggleDeveloperTools();
                e.Handled = true;
            }
        }

        private void OnHostWindowClosed(object sender, EventArgs e) {
            ((Window)sender).Closed -= OnHostWindowClosed;
            Dispose();
        }

        private void ForwardException(ExceptionDispatchInfo exceptionInfo) {
            // don't use invoke async, as it won't forward the exception to the dispatcher unhandled exception event
            Dispatcher.BeginInvoke((Action)(() => {
                if (!isDisposing) {
                    exceptionInfo?.Throw();
                }
            }));
        }

        private T ExecuteInUI<T>(Func<T> action) {
            return Dispatcher.Invoke(action);
        }

        private void AsyncExecuteInUI(Action action) {
            if (isDisposing) {
                return;
            }
            // use async call to avoid dead-locks, otherwise if the source action tries to to evaluate js it would block
            Dispatcher.InvokeAsync(
                () => {
                    if (!isDisposing) {
                        ExecuteWithAsyncErrorHandling(action);
                    }
                },
                DispatcherPriority.Normal,
                AsyncCancellationTokenSource.Token);
        }

        internal void InitializeBrowser(int initialWidth, int initialHeight) {
            chromium.CreateBrowser(initialWidth, initialHeight);
        }

        /// <summary>
        /// Called when the webview is requesting focus. Return false to allow the
        /// focus to be set or true to cancel setting the focus.
        /// <paramref name="isSystemEvent">True if is a system focus event, or false if is a navigation</paramref>
        /// </summary>
        protected virtual bool OnSetFocus(bool isSystemEvent) => false;
    }
}
