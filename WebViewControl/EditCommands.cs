namespace WebViewControl {

    public class EditCommands {

        private IWebViewEngine Engine { get; }

        internal EditCommands(IWebViewEngine engine) {
            Engine = engine;
        }

        public void Cut() => Engine.ExecuteEditCommand(EditCommand.Cut);

        public void Copy() => Engine.ExecuteEditCommand(EditCommand.Copy);

        public void Paste() => Engine.ExecuteEditCommand(EditCommand.Paste);

        public void SelectAll() => Engine.ExecuteEditCommand(EditCommand.SelectAll);

        public void Undo() => Engine.ExecuteEditCommand(EditCommand.Undo);

        public void Redo() => Engine.ExecuteEditCommand(EditCommand.Redo);

        public void Delete() => Engine.ExecuteEditCommand(EditCommand.Delete);
    }
}
