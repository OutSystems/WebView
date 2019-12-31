import * as React from "react";
import { ViewFrame } from "ViewFrame";
import { DragSource } from "./DragDrop/DragSource";

export interface ISomeType {
    name: string;
}

export interface IExampleViewProperties {
    click(arg: ISomeType): void;
}

export default class ExampleView extends React.Component<IExampleViewProperties> {
    constructor(props: IExampleViewProperties) {
        super(props);
    }

    public render(): JSX.Element {
        //return (
        //    <div className="wrapper">
        //        <ViewFrame key="drag-source-view" name="drag-source-view" />
        //        <hr />
        //        <ViewFrame key="drop-target-view" name="drop-target-view" />
        //    </div>
        //);
        return (
            <div className="wrapper">
                <input defaultValue="first text" />
                <input defaultValue="second text" />
            </div>
        );
    }
}