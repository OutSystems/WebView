using System.Windows;
using System.Windows.Controls;

namespace ReactViewControl {

    partial class ReactViewRender : UserControl {

        partial void ExtraInitialize() {
            Content = WebView;
        }

        public IInputElement FocusableElement => WebView.FocusableElement;
    }
}
