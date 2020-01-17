using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ReactViewControl {

    partial class ReactView : UserControl {

        partial void ExtraInitialize() {
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited, must be called after view is created otherwise view might be null

            IsVisibleChanged += OnIsVisibleChanged;

            Content = View;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, View.FocusableElement);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            // IWindowService is a WPF internal property set when component is loaded into a new window, even if the window isn't shown
            if (e.Property.Name == "IWindowService") {
                if (e.OldValue is Window oldWindow) {
                    oldWindow.IsVisibleChanged -= OnWindowIsVisibleChanged;
                }

                if (e.NewValue is Window newWindow) {
                    newWindow.IsVisibleChanged += OnWindowIsVisibleChanged;
                }
            }
        }

        private void OnWindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var window = (Window)sender;
            // this is the first event that we have available with guarantees that all the component properties have been set
            // since its not supposed to change properties once the component has been shown
            if (window.IsVisible) {
                window.IsVisibleChanged -= OnWindowIsVisibleChanged;
                TryLoadComponent();
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            // fallback when window was already shown
            if (IsVisible) {
                IsVisibleChanged -= OnIsVisibleChanged;
                TryLoadComponent();
            }
        }

        private static void AsyncExecuteInUI(Action action, bool lowPriority) {
            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke((Action) (() => {
                if (!dispatcher.HasShutdownStarted) {
                    action();
                }
            }), lowPriority ? DispatcherPriority.Background : DispatcherPriority.Normal);
        }
    }
}
