import * as React from "react";
import DragDropMediator, { IDropTargetEventArgs, DragEffects, convertToMouseButton } from "./DragDropMediator";
import { IPluginsContext, PluginsContext } from "PluginsProvider";

export interface IDropTargetProperties {
    dropTargetId: string;
}

export class DropTarget extends React.Component<IDropTargetProperties, {}> {
    public static contextType = PluginsContext;
    private dragDropMediator: DragDropMediator;
    private dropResult: DragEffects;

    constructor(props: IDropTargetProperties, context: IPluginsContext) {
        super(props, context);
        this.dragDropMediator = context.getPluginInstance<DragDropMediator>(DragDropMediator);
    }

    private createDropTargetEventArgs(event: React.DragEvent<HTMLDivElement>): IDropTargetEventArgs {
        return {
            altDown: event.altKey,
            ctrlDown: event.ctrlKey,
            shiftDown: event.shiftKey,
            mouseButton: convertToMouseButton(event.button),
            location: null,
            absolutePosition: null,
            textPosition: null
        };
    }

    private onDragEnter = (event: React.DragEvent<HTMLDivElement>): void => {
        console.log("onDragEnter");
        ////event.stopPropagation();
        ////event.preventDefault();

        //let args = this.createDropTargetEventArgs(event);
        //this.dragDropMediator.dragEnter(this.props.dropTargetId, args);
    }

    private onDragOver = (event: React.DragEvent<HTMLDivElement>): void => {
        console.log("onDragOver");

        //console.log("onDragOver");
        ////event.stopPropagation();
        ////event.preventDefault();

        //let dropEffect = "none";

        //switch (this.dropResult) {
        //    case DragEffects.Move:
        //        dropEffect = "move";
        //        break;
        //    case DragEffects.Copy:
        //        dropEffect = "copy";
        //        break;
        //    case DragEffects.Link:
        //        dropEffect = "link";
        //        break;
        //    default:
        //        break;
        //}

        //event.dataTransfer.dropEffect = dropEffect;

        ////let args = this.createDropTargetEventArgs(event);
        ////this.dropResult = await this.dragDropMediator.dragOver(this.props.id, args);
    }

    private onDragLeave = (event: React.DragEvent<HTMLDivElement>): void => {
        console.log("onDragLeave");
        ////event.stopPropagation();
        ////event.preventDefault();

        //let args = this.createDropTargetEventArgs(event);
        //this.dragDropMediator.dragLeave(this.props.dropTargetId, args);
    }

    private onDrop = (event: React.DragEvent<HTMLDivElement>): void => {
        event.preventDefault();

        console.log("onDrop");

        //let args = this.createDropTargetEventArgs(event);
        //this.dragDropMediator.drop(this.props.dropTargetId, args);
    }

    public render(): JSX.Element {
        return (
            <div onDragEnter={this.onDragEnter} onDragOver={this.onDragOver} onDragLeave={this.onDragLeave} onDrop={this.onDrop}>
                {this.props.children}
            </div>
        );
    }
}
