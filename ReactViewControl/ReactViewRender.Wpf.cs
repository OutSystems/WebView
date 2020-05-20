using System.Windows;
using System.Windows.Controls;

namespace ReactViewControl {

    partial class ReactViewRender : UserControl {

        partial void ExtraInitialize() {
            Content = WebView;
        }

        public IInputElement FocusableElement => WebView.FocusableElement;

        partial void PreloadWebView() {
            // initialize browser with full screen size to avoid html measure issues on initial render
            var width = (int)SystemParameters.FullPrimaryScreenWidth;
            var height = (int)SystemParameters.FullPrimaryScreenHeight;
            WebView.InitializeBrowser(width, height);
        }
    }
}
