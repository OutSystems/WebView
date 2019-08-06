import * as React from "react";
import ViewPlugin from "./ViewPlugin";
import { IPluginsContext } from "PluginsProvider";
import "./SubExampleView.scss";

export interface ISubExampleViewProperties {
    click(): void;
    getTime(): Promise<string>;
    readonly constantMessage: string;
}

export interface ISubExampleViewBehaviors {
    callMe(): void;
}

export default class SubExampleView extends React.Component<ISubExampleViewProperties, { time: string; dotNetCallCount: number }> implements ISubExampleViewBehaviors {

    private viewplugin: ViewPlugin;

    constructor(props: ISubExampleViewProperties, context: IPluginsContext) {
        super(props, context);
        this.initialize();
        this.viewplugin = context.getPluginInstance<ViewPlugin>(ViewPlugin);
    }

    private async initialize(): Promise<void> {
        this.state = {
            time: "-",
            dotNetCallCount: 0,
        };
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    public callMe(): void {
        this.setState(s => {
            return {
                dotNetCallCount: s.dotNetCallCount + 1
            };
        });
    }

    public componentDidMount(): void {
        this.viewplugin.notifyViewLoaded("SubExampleView");
    }

    public render(): JSX.Element {
        return (
            <div className="wrapper">
                {this.props.constantMessage}
                <br />
                Current time (+1hr): {this.state.time}<br />
                <br />
                Dot net calls count: {this.state.dotNetCallCount}
            </div>
        );
    }
}