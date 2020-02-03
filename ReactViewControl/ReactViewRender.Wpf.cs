using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ReactViewControl {

    partial class ReactViewRender : UserControl {

        partial void ExtraInitialize() {
            Content = WebView;
        }

        public IInputElement FocusableElement => WebView.FocusableElement;

        private IntPtr GetHostViewHandle() {
            var window = Application.Current.MainWindow ?? Application.Current.Windows.Cast<Window>().FirstOrDefault();
            if (window != null) {
                return new WindowInteropHelper(window).Handle;
            }
            return IntPtr.Zero;
        }
    }
}
