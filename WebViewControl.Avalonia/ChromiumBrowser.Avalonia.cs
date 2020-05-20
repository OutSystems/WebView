using Avalonia.Controls;
using Xilium.CefGlue.Avalonia;

namespace WebViewControl {

    partial class ChromiumBrowser : AvaloniaCefBrowser {

        private WindowBase hostingWindow;

        public void CreateBrowser(WindowBase hostingWindow, int width, int height) {
            if (IsBrowserInitialized) {
                return;
            }
            this.hostingWindow = hostingWindow;
            CreateBrowser(width, height);
        }

        protected override WindowBase GetHostingWindow() {
            return hostingWindow ?? base.GetHostingWindow();
        }
    }
}
