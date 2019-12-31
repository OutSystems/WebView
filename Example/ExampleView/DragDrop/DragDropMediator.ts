export enum MouseButton {
    Left = 0,
    Middle = 1,
    Right = 2
}

export function convertToMouseButton(button: number): MouseButton {
    switch (button) {
        case 0:
            return MouseButton.Left;
        case 1:
            return MouseButton.Middle;
        case 2:
            return MouseButton.Right;
        default:
            return null;
    }
}

export interface IDragDropMediatorProperties {
    dragStart(dragSourceId: string): void;
    dragEnter(dropTargetId: string, args: IDropTargetEventArgs): void;
    dragOver(dropTargetId: string, args: IDropTargetEventArgs): Promise<DragEffects>;
    dragLeave(dropTargetId: string, args: IDropTargetEventArgs): void;
    drop(dropTargetId: string, args: IDropTargetEventArgs): void;
}

export enum DragEffects {
    None,
    Copy,
    Move,
    Link
}

export enum DropLocation {
    Before,
    Over,
    After,
    Absolute,
    Text
}

export interface IAbsolutePositon {
    x: number;
    y: number;
}

export interface IDropTargetEventArgs {
    mouseButton: MouseButton;
    ctrlDown: boolean;
    altDown: boolean;
    shiftDown: boolean;
    location: DropLocation;
    absolutePosition: IAbsolutePositon;
    textPosition: number;
}

export default class DragDropMediator {
    constructor(private nativeObject: IDragDropMediatorProperties) { }

    public dragStart(dragSourceId: string): void {
        return this.nativeObject.dragStart(dragSourceId);
    }

    public dragEnter(dropTargetId: string, args: IDropTargetEventArgs): void {
        return this.nativeObject.dragEnter(dropTargetId, args);
    }

    public dragOver(dropTargetId: string, args: IDropTargetEventArgs): Promise<DragEffects> {
        return this.nativeObject.dragOver(dropTargetId, args);
    }

    public dragLeave(dropTargetId: string, args: IDropTargetEventArgs): void {
        return this.nativeObject.dragLeave(dropTargetId, args);
    }

    public drop(dropTargetId: string, args: IDropTargetEventArgs): void {
        return this.nativeObject.drop(dropTargetId, args);
    }
}