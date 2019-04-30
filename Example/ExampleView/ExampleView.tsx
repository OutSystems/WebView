import * as React from "react";
import "css!./ExampleView.css";

export interface ISomeType {
    name: string;
}

export interface IExampleViewProperties {
    click(arg: ISomeType): void;
    getTime(): Promise<string>
}

export interface IExampleViewBehaviors {
    callMe(): void;
}

export default class ExampleView extends React.Component<IExampleViewProperties, { time: string }> implements IExampleViewBehaviors {

    constructor(props: IExampleViewProperties) {
        super(props);
        this.state = {
            time: "-"
        };
        this.initialize();
    }

    private async initialize() {
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    callMe(): void {
        alert("React View says: clicked on a WPF button");
    }

    render() {
        return (
            <div className="wrapper">
                Current time: {this.state.time}<br/>
                <button onClick={() => this.props.click(null)}>Click me!</button>
            </div>
        );
    }
}