using Example.ViewAdapters;

namespace Example.DragDrop {
    class DragDropMediator: IDragSourceMediator, IDropTargetMediator {
        private readonly ViewAdapter viewAdapter;

        public DragDropMediator(ViewAdapter viewAdapter) {
            this.viewAdapter = viewAdapter;
        }

        public event DragStartedEventHandler DragStarted;
        public event DropEventHandler Drop;

        public void TriggerDragStart(object objectBeingDragged) {
            DragStarted?.Invoke(sender: this, args: new DragEventArgs(objectBeingDragged));
        }

        public void TriggerDrop(object dropTarget, DropTargetEventArgs dropTargetEventArgs) {
            Drop?.Invoke(sender: this, args: new DropEventArgs(dropTarget, dropTargetEventArgs));
        }
    }
}
