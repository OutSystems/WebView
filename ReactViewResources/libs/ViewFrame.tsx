import * as React from "react";

export interface IViewFrameProperties {
    name: string;
}

export default class ViewFrame extends React.Component<IViewFrameProperties> {

    render(): React.ReactNode {
        return <iframe name={this.props.name} src={window.location.href} frameBorder="0" style={{ width: "100%", height: "100%"}}></iframe>;
    }
}