﻿import * as React from "react";
import { ObservableListCollection } from "./ObservableCollection";
import { ViewMetadata } from "./ViewMetadata";
import { ViewPortal, ViewLifecycleEventHandler, ViewErrorHandler } from "./ViewPortal";
export { ViewLifecycleEventHandler, ViewErrorHandler } from "./ViewPortal";

interface IViewPortalsCollectionProps {
    views: ObservableListCollection<ViewMetadata>;
    viewAdded: ViewLifecycleEventHandler;
    viewRemoved: ViewLifecycleEventHandler;
    viewErrorRaised: ViewErrorHandler;
}

/**
 * Handles notifications from the views collection. Whenever a view is added or removed
 * the corresponding ViewPortal is added or removed
 * */
export class ViewPortalsCollection extends React.Component<IViewPortalsCollectionProps> {

    constructor(props: IViewPortalsCollectionProps) {
        super(props);
        props.views.addChangedListener(() => this.forceUpdate());
    }

    public shouldComponentUpdate() {
        return false;
    }

    private renderViewPortal(view: ViewMetadata) {
        return (
            <ViewPortal key={view.name}
                view={view}
                viewMounted={this.props.viewAdded}
                viewUnmounted={this.props.viewRemoved}
                viewErrorRaised={this.props.viewErrorRaised} />
        );
    }

    public render(): React.ReactNode {
        return this.props.views.items.sort((a, b) => a.name.localeCompare(b.name)).map(view => this.renderViewPortal(view));
    }
}