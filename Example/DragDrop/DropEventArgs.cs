using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.DragDrop {
    public class DropEventArgs {

        public DropEventArgs(object dropTarget, DropTargetEventArgs dropTargetEventArgs) {
            DropTarget = dropTarget;
            DropTargetEventArgs = dropTargetEventArgs;
        }

        public object DropTarget { get; }

        public DropTargetEventArgs DropTargetEventArgs { get; }
    }
}
