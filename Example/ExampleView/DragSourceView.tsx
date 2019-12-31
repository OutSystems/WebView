import * as React from "react";
import { DragSource } from "./DragDrop/DragSource";

export interface IDragSourceViewProperties {
    click(arg: string): void;
}

export default class DragSourceView extends React.Component<IDragSourceViewProperties> {
    public render(): JSX.Element {
        return (
            <DragSource dragSourceId="drag-source-view">
                <div>Drag from here</div>
            </DragSource>
        );
    }
}