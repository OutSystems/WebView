import * as React from "react";
import { webViewRootId, getStylesheets } from "./LoaderCommon"
import { ViewMetadata } from "./ViewMetadata";

export type ViewLifecycleEventHandler = (view: ViewMetadata) => void;

export interface IViewPortalProps {
    view: ViewMetadata
    viewMounted: ViewLifecycleEventHandler;
    viewUnmounted: ViewLifecycleEventHandler;
}

interface IViewPortalState {
    component: React.ReactElement;
}

/**
 * A ViewPortal is were a view is rendered. The view DOM is then moved into the appropriate placeholder.
 * This way we avoid a view being recreated (and losing state) when its ViewFrame is moved in the tree.
 * 
 * A View Frame notifies its sibling view collection when a new instance is mounted.
 * Upon mount, a View Portal is created and it will be responsible for rendering its view component in the shadow dom.
 * A view portal is persisted until its View Frame counterpart disappears.
 * */
export class ViewPortal extends React.Component<IViewPortalProps, IViewPortalState> {

    private head: Element;
    private shadowRoot: Element;

    constructor(props: IViewPortalProps) {
        super(props);

        this.state = { component: null! };
        
        this.shadowRoot = props.view.placeholder.attachShadow({ mode: "open" }).getRootNode() as Element;

        props.view.renderHandler = component => this.renderPortal(component);
    }

    private renderPortal(component: React.ReactElement) {
        return new Promise<void>(resolve => this.setState({ component }, resolve));
    }

    public shouldComponentUpdate(nextProps: IViewPortalProps, nextState: IViewPortalState) {
        // only update if the component was set (once)
        return this.state.component === null && nextState.component !== this.state.component;
    }

    public componentDidMount() {
        this.props.view.head = this.head;

        const styleResets = document.createElement("style");
        styleResets.innerHTML = ":host { all: initial; display: block; }";

        this.head.appendChild(styleResets);

        // get sticky stylesheets
        const stylesheets = getStylesheets(document.head).filter(s => s.dataset.sticky === "true");
        stylesheets.forEach(s => this.head.appendChild(document.importNode(s, true)));

        this.props.viewMounted(this.props.view);
    }

    public componentWillUnmount() {
        this.props.viewUnmounted(this.props.view);
    }

    public render(): React.ReactNode {
        return ReactDOM.createPortal(
            <>
                <head ref={e => this.head = e!}>
                </head>
                <body>
                    <div id={webViewRootId} ref={e => this.props.view.root = e!}>
                        {this.state.component ? this.state.component : null}
                    </div>
                </body>
            </>,
            this.shadowRoot);
    }
}