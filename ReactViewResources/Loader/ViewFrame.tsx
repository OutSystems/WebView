import * as React from "react";
import { ViewMetadata, Placeholder, Task } from "./LoaderCommon";
import { ObservableListCollection } from "./ObservableCollection";

type ViewFrameProps = { name: string, className: string };

export class ViewFrame extends React.Component<ViewFrameProps, {}, ViewMetadata> {

    private componentGuid: string;
    private container: HTMLElement;

    constructor(props: ViewFrameProps, context: ViewMetadata) {
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

    private get parentView(): ViewMetadata {
        return this.context as ViewMetadata;
    }

    public componentDidMount() {
        const childView: ViewMetadata = {
            name: this.props.name,
            componentGuid: this.componentGuid,
            isMain: false,
            placeholder: this.container,
            modules: new Map<string, any>(),
            nativeObjectNames: [],
            pluginsLoadTask: new Task(),
            scriptsLoadTasks: new Map<string, Task<void>>(),
            childViews: new ObservableListCollection<ViewMetadata>(),
            parentView: this.parentView
        };
        this.parentView.childViews.add(childView);
    }

    public componentWillUnmount() {
        //this.parentView.childPlaceholders.remove(this.placeholder);
    }

    public render() {
        return <div ref={e => this.container = e!} className={this.props.className} />;
    }
}

window["ViewFrame"] = { ViewFrame: ViewFrame };
