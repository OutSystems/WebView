import * as React from "react";
import * as ViewPlugin from "./ViewPlugin";
import "./SubExampleView.scss";

export interface ISubExampleViewProperties {
    click(): void;
    getTime(): Promise<string>;
    readonly constantMessage: string;
}

export interface ISubExampleViewBehaviors {
    callMe(): void;
}

export default class SubExampleView extends React.Component<ISubExampleViewProperties, { time: string, dotNetCallCount: number }> implements ISubExampleViewBehaviors {

    constructor(props: ISubExampleViewProperties) {
        super(props);
        this.initialize();
    }

    private async initialize() {
        this.state = {
            time: "-",
            dotNetCallCount: 0,
        };
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    callMe(): void {
        this.setState(s => {
            return {
                dotNetCallCount: s.dotNetCallCount + 1
            };
        });
    }

    componentDidMount(): void {
        ViewPlugin.notifyViewLoaded("SubExampleView");
    }

    render() {
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