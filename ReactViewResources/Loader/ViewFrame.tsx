import * as React from "react";
import { ObservableListCollection } from "./ObservableCollection";
import { Task } from "./Task";
import { ViewContext } from "./ViewContext";
import { ViewMetadata } from "./ViewMetadata";
import { IViewFrameProps } from "./../../ViewGenerator/contentFiles/node_modules/@types/ViewFrame";

/**
 * Placeholder were a child view is mounted.
 * */
export class ViewFrame extends React.Component<IViewFrameProps, {}, ViewMetadata> {

    private static generation = 0;

    private generation: number;
    private placeholder: Element;
    private replacement: Element;

    static contextType = ViewContext;

    constructor(props: IViewFrameProps, context: ViewMetadata) {
        super(props, context);
        if (props.name === "") {
            throw new Error("View Frame name must be specified (not empty)");
        }

        // keep track of this frame generation, so that we can keep tracking the most recent frame instance
        this.generation = ++ViewFrame.generation;

        const view = this.getView();
        if (view) {
            // update the existing view generation
            view.generation = this.generation;
        }
    }

    public shouldComponentUpdate(): boolean {
        // prevent component updates
        return false;
    }

    private get parentView(): ViewMetadata {
        return this.context as ViewMetadata;
    }

    private getView(): ViewMetadata | undefined {
        return this.parentView.childViews.items.find(c => c.name === this.props.name);
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
            name: this.props.name,
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
