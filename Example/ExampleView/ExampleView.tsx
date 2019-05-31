import * as React from "react";
import * as ReactDOM from "react-dom";
import "css!./ExampleView.css";
import ViewFrame from "ViewFrame";

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
    readonly constantMessage: string;
    readonly image: ImageKind;
}

export interface IExampleViewBehaviors {
    callMe(): void;
}

export default class ExampleView extends React.Component<IExampleViewProperties, { time: string }> implements IExampleViewBehaviors {

    constructor(props: IExampleViewProperties) {
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
                {this.props.image === ImageKind.Beach ? <img className="image" src="beach.jpg" /> : null}
                <br />
                <button onClick={() => this.props.click(null)}>Click me!</button>
                <br />
                <ViewFrame name="test" />
            </div>
        );
    }
}