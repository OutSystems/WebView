import * as React from "react";
import { DropTarget } from "./DragDrop/DropTarget";

export interface IDropTargetViewProperties {
    click(arg: string): void;
}

export default class DropTargetView extends React.Component<IDropTargetViewProperties> {
    public render(): JSX.Element {
        return (
            <DropTarget dropTargetId="drop-target-view">
                <div style={{ width: "350px", height: "70px", padding: "10px", border: "1px solid black" }}></div>
            </DropTarget>
        );
    }
}