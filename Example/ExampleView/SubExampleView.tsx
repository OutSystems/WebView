import * as React from "react";
import "css!./SubExampleView.css";

export interface ISubExampleViewProperties {
    click(): void;
    getTime(): Promise<string>;
    readonly constantMessage: string;
}

export interface ISubExampleViewBehaviors {
    callMe(): void;
}

export default class SubExampleView extends React.Component<ISubExampleViewProperties, { time: string }> implements ISubExampleViewBehaviors {

    constructor(props: ISubExampleViewProperties) {
        super(props);
        this.initialize();
    }

    private async initialize() {
        this.state = {
            time: "-"
        };
        let time = await this.props.getTime();
        this.setState({ time: time });
    }

    callMe(): void {
        alert("React View says: clicked on a WPF button");
    }

    render() {
        return (
            <div className="wrapper">
                {this.props.constantMessage}
                <br />
                Current time: {this.state.time}<br />
                <br />
            </div>
        );
    }
}