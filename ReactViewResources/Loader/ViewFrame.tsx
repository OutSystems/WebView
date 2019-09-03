import * as React from "react";
import { ViewContext } from "./LoaderCommon";

type ViewFrameProps = { name: string, className: string };

export class ViewFrame extends React.Component<ViewFrameProps, {}, ViewContext> {

    private componentGuid: string;
    private container: HTMLElement;

    constructor(props: ViewFrameProps, context: ViewContext) {
        super(props, context);
        if (props.name === "") {
            throw new Error("View Frame name must be specified (not empty)");
        }
        
        this.componentGuid = performance.now() + "" + Math.random();
        //const view = Common.getView(props.name);
        //if (view) {
        //    // if there's already a view with the same name it will be replaced with this new instance
        //    view.componentGuid = this.componentGuid;
        //}
    }

    public shouldComponentUpdate(): boolean {
        // prevent component updates, it will be updated explicitly
        return false;
    }

    public componentDidMount() {
        (this.context as ViewContext).addOrReplaceSubView(this.props.name, this.container);
    }

    public componentWillUnmount() {
        (this.context as ViewContext).removeSubView(this.props.name);
    }

    public render() {
        return <div ref={e => this.container = e!} className={this.props.className} />;
    }
}

window["ViewFrame"] = { ViewFrame: ViewFrame };
