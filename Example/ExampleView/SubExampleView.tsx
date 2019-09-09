import * as React from "react";
import ViewPlugin from "./ViewPlugin";
import { IPluginsContext, PluginsContext } from "PluginsProvider";
import "./SubExampleView.scss";
import { ResourceLoader } from "ResourceLoader";

export interface ISubExampleViewProperties {
    click(): void;
    getTime(): Promise<string>;
    readonly constantMessage: string;
}

export interface ISubExampleViewBehaviors {
    callMe(): void;
}

class SubExampleComponent extends React.Component<{}, {}, IPluginsContext> {
    
    public static contextType = PluginsContext;

    constructor(props: {}, context: IPluginsContext) {
        super(props, context);
    }

    render(): JSX.Element {
        return <div>Plugins provider available: {(this.context as IPluginsContext).getPluginInstance ? "yes" : "no"}</div>;
    }
}

export default class SubExampleView extends React.Component<ISubExampleViewProperties, { time: string; dotNetCallCount: number, buttonClicksCount: number }> implements ISubExampleViewBehaviors {

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
            buttonClicksCount: 0
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
                <br />
                Button clicks count: {this.state.buttonClicksCount}
                <br />
                <SubExampleComponent />
                <br />
                Custom resource:
                <ResourceLoader.Consumer>
                    {url => <img src={url("Completed.png")} />}
                </ResourceLoader.Consumer>
                <br/>
                <button onClick={() => this.setState(s => { return { buttonClicksCount: s.buttonClicksCount + 1 }; })}>Click me!</button>&nbsp;
            </div>
        );
    }
}