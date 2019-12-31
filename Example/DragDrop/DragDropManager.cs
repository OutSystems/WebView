using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.DragDrop {
    static class DragDropManager {
        public static object ObjectBeingDragged { get; private set; }

        public static void RegisterDragSourceMediator(IDragSourceMediator dragSourceMediator) {
            dragSourceMediator.DragStarted += OnDragSourceMediatorDragStarted;
        }

        public static void RegisterDropTargetMediator(IDropTargetMediator dropTargetMediator) {
            dropTargetMediator.Drop += OnDropTargetMediatorDrop;
        }

        private static void OnDragSourceMediatorDragStarted(IDragSourceMediator sender, DragEventArgs args) {
            ObjectBeingDragged = args.ObjectBeingDragged;
        }

        private static void OnDropTargetMediatorDrop(IDropTargetMediator sender, DropEventArgs args) {
            ObjectBeingDragged = null;
        }
    }
}
