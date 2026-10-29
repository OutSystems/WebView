namespace WebViewControl {

    public class EditCommands {

        private WebView WebView { get; }

        internal EditCommands(WebView webView) {
            WebView = webView;
        }

        public void Cut() => WebView.PerformEditCommand(EditCommand.Cut);

        public void Copy() => WebView.PerformEditCommand(EditCommand.Copy);

        public void Paste() => WebView.PerformEditCommand(EditCommand.Paste);

        public void SelectAll() => WebView.PerformEditCommand(EditCommand.SelectAll);

        public void Undo() => WebView.PerformEditCommand(EditCommand.Undo);

        public void Redo() => WebView.PerformEditCommand(EditCommand.Redo);

        public void Delete() => WebView.PerformEditCommand(EditCommand.Delete);
    }

    internal enum EditCommand {
        Cut,
        Copy,
        Paste,
        SelectAll,
        Undo,
        Redo,
        Delete
    }

    partial class WebView {

        internal void PerformEditCommand(EditCommand command) {
            RunInUI(() => {
                if (isDisposing || !browser.IsCreated) {
                    return;
                }
                switch (command) {
                    case EditCommand.Cut:
                        browser.Cut();
                        break;
                    case EditCommand.Copy:
                        browser.Copy();
                        break;
                    case EditCommand.Paste:
                        browser.Paste();
                        break;
                    case EditCommand.SelectAll:
                        browser.SelectAll();
                        break;
                    case EditCommand.Undo:
                        ExecuteScript("document.execCommand('undo')");
                        break;
                    case EditCommand.Redo:
                        ExecuteScript("document.execCommand('redo')");
                        break;
                    case EditCommand.Delete:
                        ExecuteScript("document.execCommand('delete')");
                        break;
                }
            });
        }
    }
}
