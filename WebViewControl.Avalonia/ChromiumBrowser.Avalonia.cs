using Avalonia.Controls;
using Xilium.CefGlue.Avalonia;

namespace WebViewControl {

    partial class ChromiumBrowser : AvaloniaCefBrowser {

        protected override Window GetHostingWindow() {
            return HostingWindow ?? base.GetHostingWindow();
        }

        public Window HostingWindow { get; set; }
    }
}
