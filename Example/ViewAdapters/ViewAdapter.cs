using Example.DragDrop;
using ReactViewControl;

namespace Example.ViewAdapters {
    class ViewAdapter {
        private readonly ReactView reactView;
        private readonly string frameName;
        private readonly IDragSourceMediator dragSourceMediator;
        private readonly IDropTargetMediator dropTargetMediator;

        public ViewAdapter(ReactView reactView, string frameName = "") {
            this.reactView = reactView;
            this.frameName = frameName;

            var dragDropMediator = new DragDrop.DragDropMediator(this);
            dragSourceMediator = dragDropMediator;
            dropTargetMediator = dragDropMediator;

            DragDropManager.RegisterDragSourceMediator(dragSourceMediator);
            DragDropManager.RegisterDropTargetMediator(dropTargetMediator);

            var viewDragDropMediator = reactView.WithPlugin<DragDropMediator>(frameName);
            viewDragDropMediator.DragStart += OnDragDropMediatorDragStart;
            //viewDragDropMediator.DragEnter += OnDragDropMediatorDragEnter;
            //viewDragDropMediator.DragOver += OnDragDropMediatorDragOver;
            //viewDragDropMediator.DragLeave += OnDragDropMediatorDragLeave;
            viewDragDropMediator.Drop += OnDragDropMediatorDrop;
        }

        private void OnDragDropMediatorDragStart(string dragSourceId) {
            reactView.Dispatcher.Invoke(() => dragSourceMediator.TriggerDragStart(dragSourceId));
        }

        private void OnDragDropMediatorDrop(string dropTargetId, DropTargetEventArgs args) {
            reactView.Dispatcher.Invoke(() => dropTargetMediator.TriggerDrop(dropTargetId, args));
        }
    }
}
