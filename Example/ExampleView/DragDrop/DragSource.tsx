import * as React from "react";
import DragDropMediator from "./DragDropMediator";
import { IPluginsContext, PluginsContext } from "PluginsProvider";

export interface IDragSourceProperties {
    dragSourceId: string;
}

export class DragSource extends React.Component<IDragSourceProperties, {}> {
    public static contextType = PluginsContext;
    private dragDropMediator: DragDropMediator;

    constructor(props: IDragSourceProperties, context: IPluginsContext) {
        super(props, context);
        this.dragDropMediator = context.getPluginInstance<DragDropMediator>(DragDropMediator);
    }

    private onDragStart = (event: React.DragEvent<HTMLDivElement>) => {
        console.log("onDragStart");

        //event.dataTransfer.setData('text', event.target.);

        //event.stopPropagation();

        //event.dataTransfer.effectAllowed = "all";
        //event.dataTransfer.setData("text/plain", this.props.dragSourceId);
        //this.dragDropMediator.dragStart(this.props.dragSourceId);
    };

    private onDragEnd = (event: React.DragEvent<HTMLDivElement>) => {
        console.log("onDragEnd");
    };

    public render(): JSX.Element {
        return (
            <div draggable={true} onDragStart={this.onDragStart} onDragEnd={this.onDragEnd}>
                {this.props.children}
            </div>
        );
    }
}