import * as React from "react";
import { ObservableListCollection } from "./ObservableCollection";
import { ViewMetadata } from "./ViewMetadata";
import { ViewPortal } from "./ViewPortal";

interface IViewPortalsCollectionProps {
    views: ObservableListCollection<ViewMetadata>;
    viewAddedHandler: (view: ViewMetadata) => void;
}

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
            .map(view => <ViewPortal key={ view.name } view = { view } viewAddedHandler = { this.props.viewAddedHandler } />);
    }
}