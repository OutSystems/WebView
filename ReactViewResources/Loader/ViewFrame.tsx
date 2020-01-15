import * as React from "react";
import { ObservableListCollection } from "./ObservableCollection";
import { Task } from "./Task";
import { ViewContext } from "./ViewContext";
import { ViewMetadata } from "./ViewMetadata";
import { IViewFrameProps } from "ViewFrame";

/**
 * Placeholder were a child view is mounted.
 * */
export class ViewFrame<T> extends React.Component<IViewFrameProps<T>, {}, ViewMetadata> {

    private static generation = 0;

    private generation: number;
    private placeholder: Element;
    private replacement: Element;

    static contextType = ViewContext;

    constructor(props: IViewFrameProps<T>, context: ViewMetadata) {
        super(props, context);
        if (props.name === "") {
            throw new Error("View Frame name must be specified (not empty)");
        }

        if (!/^[A-Za-z_][A-Za-z0-9_]*/.test(props.name as string)) {
            // must be a valid js symbol name
            throw new Error("View Frame name can only contain letters, numbers or _");
        }

        // keep track of this frame generation, so that we can keep tracking the most recent frame instance
        this.generation = ++ViewFrame.generation;

        const view = this.getView();
        if (view) {
            // update the existing view generation
            view.generation = this.generation;
        }
    }

    private get fullName() {
        const parentName = this.parentView.name;
        return (parentName ? (parentName + ".") : "") + this.props.name;
    }

    public shouldComponentUpdate(): boolean {
        // prevent component updates
        return false;
    }

    private get parentView(): ViewMetadata {
        return this.context as ViewMetadata;
    }

    private getView(): ViewMetadata | undefined {
        const fullName = this.fullName;
        return this.parentView.childViews.items.find(c => c.name === fullName);
    }

    public componentDidMount() {
        const existingView = this.getView();
        if (existingView) {
            // there's a view already rendered, insert in current frame's placeholder
            this.replacement = existingView.placeholder;
            this.placeholder.parentElement!.replaceChild(this.replacement, this.placeholder);
            return;
        }

        const childView: ViewMetadata = {
            name: this.fullName,
            generation: this.generation,
            isMain: false,
            placeholder: this.placeholder,
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
        if (this.replacement) {
            // put back the original container, otherwise react will complain
            this.replacement.parentElement!.replaceChild(this.placeholder, this.replacement);
        }

        const existingView = this.getView();
        if (existingView && this.generation === existingView.generation) {
            // this is the most recent frame - meaning it was not replaced by another one - so the view should be removed
            this.parentView.childViews.remove(existingView);
        }
    }

    public render() {
        return <div ref={e => this.placeholder = e!} className={this.props.className} />;
    }
}

window["ViewFrame"] = { ViewFrame: ViewFrame };
