namespace Example.DragDrop {

    public class DragEventArgs {
        public DragEventArgs(object objectBeingDragged) {
            ObjectBeingDragged = objectBeingDragged;
        }

        public object ObjectBeingDragged { get; }
    }
}
