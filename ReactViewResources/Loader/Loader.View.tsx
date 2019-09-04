import * as React from "react";
import * as ReactDOM from "react-dom";
import { ViewMetadata, PluginsContext, Task, Placeholder, getStylesheets, webViewRootId } from "./LoaderCommon";
import { ViewFrame } from "./ViewFrame";
import { ObservableListCollection } from "./ObservableCollection";

const pluginsContext = React.createContext<PluginsContext>(null!);
const viewContext = React.createContext<ViewMetadata>(null!);

const pluginsProviderModuleName: string = "PluginsProvider";

ViewFrame.contextType = viewContext;
window[pluginsProviderModuleName] = { PluginsContext: pluginsContext };

export interface IViewPortalProps {
    view: ViewMetadata
    viewAddedHandler: (view: ViewMetadata) => void;
}

interface IViewPortalState {
    component: React.ReactElement;
}

class ViewPortal extends React.Component<IViewPortalProps, IViewPortalState> {

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
        this.props.viewAddedHandler(this.props.view);
        this.renderShadowView();
    }

    public componentDidUpdate() {
        this.renderShadowView();
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

interface IViewPortalsCollectionProps {
    views: ObservableListCollection<ViewMetadata>;
    viewAddedHandler: (view: ViewMetadata) => void;
}

class ViewPortalsCollection extends React.Component<IViewPortalsCollectionProps> {
    
    constructor(props: IViewPortalsCollectionProps) {
        super(props);
        props.views.addChangedListener(() => this.forceUpdate());
    }

    public shouldComponentUpdate() {
        return false;
    }

    public render(): React.ReactNode {
        return this.props.views.items.sort((a, b) => a.name.localeCompare(b.name))
            .map(view => <ViewPortal key={view.name} view={view} viewAddedHandler={this.props.viewAddedHandler} />);
    }
}

export function createView(componentClass: any, properties: {}, view: ViewMetadata, componentName: string, viewAddedHandler: (view: ViewMetadata) => void) {
    componentClass.contextType = pluginsContext;

    return (
        React.createElement(viewContext.Provider, { value: view },
            React.createElement(pluginsContext.Provider, { value: new PluginsContext(Array.from(view.modules.values())) },
                <>
                    <ViewPortalsCollection views={view.childViews} viewAddedHandler={viewAddedHandler} />
                    {React.createElement(componentClass, { ref: e => view.modules.set(componentName, e), ...properties })}
                </>
            )
        )
    );
}

export function renderMainView(children: React.ReactElement, container: Element): Promise<void> {
    return new Promise<void>(resolve => ReactDOM.hydrate(children, container, resolve));
}