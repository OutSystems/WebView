import * as React from "react";
import "ViewFrame";
import ViewPlugin from "./ViewPlugin";
import { IPluginsContext } from "PluginsProvider";
import "./ExampleView.scss";

export interface ISomeType {
    name: string;
}

export enum ImageKind {
    None,
    Beach
}

export interface IExampleViewProperties {
    click(arg: ISomeType): void;
    getTime(): Promise<string>;
    viewMounted(): void;
    readonly constantMessage: string;
    readonly image: ImageKind;
}

export interface IExampleViewBehaviors {
    callMe(): void;
}

export default class ExampleView extends React.Component<IExampleViewProperties, { time: string, showSubView: boolean }> implements IExampleViewBehaviors {

    private viewplugin: ViewPlugin;

    constructor(props: IExampleViewProperties, context: IPluginsContext) {
        super(props, context);
        this.initialize();
        this.viewplugin = context.getPluginInstance<ViewPlugin>(ViewPlugin);
    }

    private async initialize() {
        this.state = {
            time: "-",
            showSubView: true
        };
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    callMe(): void {
        alert("React View says: clicked on a WPF button");
    }

    componentDidMount(): void {
        this.viewplugin.notifyViewLoaded("ExampleView");
    }

    private onMountSubViewClick = () => {
        let show = !this.state.showSubView;
        if (show) {
            this.props.viewMounted();
        }
        this.setState({ showSubView: show });
    }

    render() {
        return (
            <div className="wrapper">
                {this.props.constantMessage}
                <br />
                Current time: {this.state.time}<br />
                <br />
                {this.props.image === ImageKind.Beach ? <img className="image" src="beach.jpg" /> : null}
                <br />
                <div className="buttons-bar">
                    <button onClick={() => this.props.click(null)}>Click me!</button>&nbsp;
                    <button onClick={this.onMountSubViewClick}>{this.state.showSubView ? "Unmount" : "Mount"} subview</button>
                </div>
                <br />
                {this.state.showSubView ? <view-frame id="test" /> : null}
            </div>
        );
    }
}