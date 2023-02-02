using Xilium.CefGlue;

namespace WebViewControl {

    public class EditCommands {

        private ChromiumBrowser ChromiumBrowser { get; }

        internal EditCommands(ChromiumBrowser chromiumBrowser) {
            ChromiumBrowser = chromiumBrowser;
        }

        private CefFrame GetFocusedFrame() => ChromiumBrowser.GetBrowser()?.GetFocusedFrame() ?? ChromiumBrowser.GetBrowser()?.GetMainFrame();

        public void Cut() => GetFocusedFrame()?.Cut();

        public void Copy() => GetFocusedFrame()?.Copy();

        public void Paste() => GetFocusedFrame()?.Paste();

        public void SelectAll() => GetFocusedFrame()?.SelectAll();

        public void Undo() => GetFocusedFrame()?.Undo();

        public void Redo() => GetFocusedFrame()?.Redo();

        public void Delete() => GetFocusedFrame()?.Delete();
    }
}
