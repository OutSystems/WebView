using System;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xilium.CefGlue.Common;

namespace WebViewControl {

    partial class WebView : Control {

        private static bool osrEnabled = true;

        partial void ExtraInitialize() {
            VisualChildren.Add(chromium);
        }
        
        public static bool OsrEnabled { 
            get => osrEnabled;
            set {
                if (CefRuntimeLoader.IsLoaded) {
                    throw new InvalidOperationException($"Cannot set {nameof(OsrEnabled)} after WebView engine has been loaded");
                }
                osrEnabled = value;
            }
        }

        internal IInputElement FocusableElement => chromium;

        private bool IsInDesignMode => false;

        protected override void OnKeyDown(KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                ToggleDeveloperTools();
                e.Handled = true;
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
            if (e.Root is Window window) {
                // need to subscribe the event this way because close gets closed after all elements get detached
                window.AddHandler(Window.WindowClosedEvent, (EventHandler<RoutedEventArgs>) OnHostWindowClosed);
            }
            base.OnAttachedToVisualTree(e);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            if (e.Root is Window window) {
                window.RemoveHandler(Window.WindowClosedEvent, (EventHandler<RoutedEventArgs>) OnHostWindowClosed);
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void OnHostWindowClosed(object sender, RoutedEventArgs eventArgs) {
            ((Window)sender).RemoveHandler(Window.WindowClosedEvent, (EventHandler<RoutedEventArgs>)OnHostWindowClosed);
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

        internal void InitializeBrowser(WindowBase hostingWindow, int initialWidth, int initialHeight) {
            chromium.CreateBrowser(hostingWindow, initialWidth, initialHeight);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) {
            e.Handled = true;
            chromium.Focus();
        }
    }
}
