import * as React from "react";
import { ObservableListCollection } from "./ObservableCollection";
import { ViewMetadata } from "./ViewMetadata";
import { ViewPortal, ViewLifecycleEventHandler } from "./ViewPortal";
export { ViewLifecycleEventHandler } from "./ViewPortal";

interface IViewPortalsCollectionProps {
    views: ObservableListCollection<ViewMetadata>;
    viewAdded: ViewLifecycleEventHandler;
    viewRemoved: ViewLifecycleEventHandler;
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

    public render(): React.ReactNode {
        return this.props.views.items.sort((a, b) => a.name.localeCompare(b.name))
            .map(view => <ViewPortal key={view.name} view={view} viewMounted={this.props.viewAdded} viewUnmounted={this.props.viewRemoved} />);
    }
}