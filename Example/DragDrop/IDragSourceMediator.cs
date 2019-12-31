namespace Example.DragDrop {
    public delegate void DragStartedEventHandler(IDragSourceMediator sender, DragEventArgs args);

    public interface IDragSourceMediator {
        event DragStartedEventHandler DragStarted;

        void TriggerDragStart(object objectBeingDragged);
    }
}
