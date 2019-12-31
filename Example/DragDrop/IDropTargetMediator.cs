namespace Example.DragDrop {
    public delegate void DropEventHandler(IDropTargetMediator sender, DropEventArgs args);

    public interface IDropTargetMediator {
        event DropEventHandler Drop;

        void TriggerDrop(object dropTargetId, DropTargetEventArgs dropTargetEventArgs);
    }
}
