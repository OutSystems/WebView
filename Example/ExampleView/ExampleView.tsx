import * as React from "react";
import "css!./ExampleView.css";

export interface ISomeType {
    name: string;
}

export interface IExampleViewProperties {
    click(arg: ISomeType): void;
}

export interface IExampleViewBehaviors {
    callMe(): void;
}

export default class ExampleView extends React.Component<IExampleViewProperties, {}> implements IExampleViewBehaviors {

    callMe(): void {
        alert("React View says: clicked on a WPF button");
    }

    render() {
        return <button onClick={() => this.props.click(null)}>Click me!</button>
    }
}