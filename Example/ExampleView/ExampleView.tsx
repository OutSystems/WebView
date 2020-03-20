import * as React from "react";
import { ViewFrame } from "ViewFrame";
import ViewPlugin from "./ViewPlugin";
import { IPluginsContext } from "PluginsProvider";
import * as styles from "./Example.export.scss";
import "./ExampleView.scss";
import * as Image from "./beach.jpg";
import { ResourceLoader } from "ResourceLoader";
import SubExampleView from "./SubExampleView";

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


export interface IChildViews {
    SubView: SubExampleView;
}

enum SubViewShowStatus {
    Show,
    ShowWrapped,
    Hide
}

export default class ExampleView extends React.Component<IExampleViewProperties, { time: string; subViewShowStatus: SubViewShowStatus }> implements IExampleViewBehaviors {

    private viewplugin: ViewPlugin;

    constructor(props: IExampleViewProperties, context: IPluginsContext) {
        super(props, context);
        this.initialize();
        this.viewplugin = context.getPluginInstance<ViewPlugin>(ViewPlugin);
    }

    private async initialize(): Promise<void> {
        this.state = {
            time: "-",
            subViewShowStatus: SubViewShowStatus.Show
        };
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    public callMe(): void {
        alert("React View says: clicked on a WPF button");
    }

    public componentDidMount(): void {
        this.viewplugin.notifyViewLoaded("ExampleView");
        if (this.state.subViewShowStatus !== SubViewShowStatus.Hide) {
            this.props.viewMounted();
        }
    }

    private onMountSubViewClick = () => {
        let next = (this.state.subViewShowStatus + 1) % 3;
        if (next === SubViewShowStatus.Show) {
            this.props.viewMounted();
        }
        this.setState({ subViewShowStatus: next });
    }

    private renderViewFrame() {
        return <ViewFrame<IChildViews> key="test_frame" name="SubView" />;
    }

    private renderSubView() {
        switch (this.state.subViewShowStatus) {
            case SubViewShowStatus.Show:
                return <div>{this.renderViewFrame()}</div>;
            case SubViewShowStatus.ShowWrapped:
                return this.renderViewFrame();
            default:
                return null;
        }
    }

    public render(): JSX.Element {
        return (
            <div className="wrapper">
                {this.props.constantMessage}
                <br />
                Current time: {this.state.time}
                <br />
                This is a shared SASS varible value: '{styles.exportedVariable}'
                <br />
                {this.props.image === ImageKind.Beach ? <img className="image" src={Image} /> : null}
                <br />
                <div className="buttons-bar">
                    <button onClick={() => this.props.click(null)}>Click me!</button>&nbsp;
                    <button onClick={this.onMountSubViewClick}>Mount/Wrap/Hide child view</button>
                </div>
                Custom resource:
                <ResourceLoader.Consumer>
                    {url => <img src={url("Ok.png")} />}
                </ResourceLoader.Consumer>
                <br />
                {this.renderSubView()}
            </div>
        );
    }
}