import * as React from "react";
import { webViewRootId } from "./LoaderCommon"
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

export class ViewPortal extends React.Component<IViewPortalProps, IViewPortalState> {

    private container: Element;
    private shadowRoot: Element;

    constructor(props: IViewPortalProps) {
        super(props);

        this.state = { component: null! };
        
        //this.container = document.createElement("div");
        this.shadowRoot = props.view.placeholder.attachShadow({ mode: "open" }).getRootNode() as Element;
        //this.shadowRoot = this.container.attachShadow({ mode: "open" }).getRootNode() as Element;

        props.view.componentRenderHandler = component => this.renderPortal(component);
    }

    private renderPortal(component: React.ReactElement) {
        return new Promise<void>(resolve => this.setState({ component }));
    }

    public shouldComponentUpdate(nextProps: IViewPortalProps, nextState: IViewPortalState) {
        return this.state.component === null && nextState.component !== this.state.component;
    }

    public componentDidMount() {
        this.props.viewMounted(this.props.view);
        this.renderShadowView();
    }

    public componentDidUpdate() {
        this.renderShadowView();
    }

    public componentWillUnmount() {
        this.props.viewUnmounted(this.props.view);
    }

    private renderShadowView() {
        if (!this.state.component) {
            return;
        }
        // get sticky stylesheets
        //const stylesheets = getStylesheets(document.head).filter(s => s.dataset.sticky === "true");

        //const head = document.createElement("head");

        //// TODO import using import method
        //head.innerHTML = (
        //    "<style>:host { all: initial; display: block; }</style>\n" + // reset inherited css properties
        //    stylesheets.map(s => s.outerHTML).join("\n") // import sticky stylesheets into this view 
        //);

        //this.shadowRoot.appendChild(head);

        //const body = document.createElement("body");
        //body.appendChild(this.shadowRoot.firstElementChild!);
        //this.shadowRoot.appendChild(body);

        //const root = document.createElement("div");
        //root.id = webViewRootId;
        //body.appendChild(root);

        //
        const view = this.props.view;
        //view.root = root;
        //view.head = head;

        //view.placeholder.parentElement!.replaceChild(this.container, view.placeholder);
    }

    public render(): React.ReactNode {
        return ReactDOM.createPortal(
            <>
                <head ref={e => this.props.view.head = e!}>
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