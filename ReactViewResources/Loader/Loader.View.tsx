import * as React from "react";
import * as ReactDOM from "react-dom";
import { ViewMetadata, PluginsContext, ViewContext, IViewCollection, Task } from "./LoaderCommon";
import { ViewFrame } from "./ViewFrame";

const pluginsContext = React.createContext<PluginsContext | null>(null);;
const viewContext = React.createContext<ViewContext>(new ViewContext("", null!));

const pluginsProviderModuleName: string = "PluginsProvider";

ViewFrame.contextType = viewContext;
window[pluginsProviderModuleName] = { PluginsContext: pluginsContext };

export interface IViewProps {
    component: React.ComponentClass;
    placeholder: HTMLElement;
    name: string;
}

class ViewPortal extends React.Component<IViewProps, {}> {

    private container: HTMLDivElement;
    private shadowRoot: Element;

    constructor(props: IViewProps) {
        super(props);
        this.container = document.createElement("div");
        this.shadowRoot = this.container.attachShadow({ mode: "open" }).getRootNode() as Element;
    }

    public shouldComponentUpdate() {
        return false;
    }

    public get name() {
        return this.props.name;
    }

    public componentDidMount() {
        //// create an open shadow-dom, so that bubbled events expose the inner element
        //const shadowRoot = this.container.attachShadow({ mode: "open" }).getRootNode() as Element;

        //// get sticky stylesheets
        //const mainView = Common.getView(Common.mainFrameName);
        //const stylesheets = Common.getStylesheets(mainView.head).filter(s => s.dataset.sticky === "true");

        //const head = document.createElement("head");

        //// TODO import using import method
        //head.innerHTML = (
        //    "<style>:host { all: initial; display: block; }</style>\n" + // reset inherited css properties
        //    stylesheets.map(s => s.outerHTML).join("\n") // import sticky stylesheets into this view 
        //);

        //shadowRoot.appendChild(head);

        //const body = document.createElement("body");
        //shadowRoot.appendChild(body);

        //const root = document.createElement("div");
        //root.id = Common.webViewRootId;
        //body.appendChild(root);

        //const view = Common.getView(this.props.name);
        //if (view && view.componentGuid === this.componentGuid) {
        //    // update view
        //    view.head = head;
        //    view.root = root;
        //    view.renderContent = this.renderContent;
        //} else {
        //    Common.addView(this.props.name, this.componentGuid, false, root, head, this.renderContent);
        //}

        ////this.forceUpdate(() => {
        //    //if (!this.root || !this.head) {
        //    //    throw new Error("Expected root and head to be set");
        //    //}
        //    //const view = Common.getView(this.props.name);
        //    //if (view && view.componentGuid === this.componentGuid) {
        //    //    // update view
        //    //    view.head = this.head;
        //    //    view.root = this.root;
        //    //    view.renderContent = this.renderChildren;
        //    //} else {
        //    //    Common.addView(this.props.name, this.componentGuid, false, this.root, this.head, this.renderChildren);
        //    //}
        ////this.renderPortal();
        ////});
    }

    public render(): React.ReactNode {
        const component = React.createElement(this.props.component);
        return ReactDOM.createPortal(component, this.shadowRoot);
    }
}

class ViewPortalsCollection extends React.Component implements IViewCollection {
    
    private views: ViewMetadata[] = [];

    public shouldComponentUpdate() {
        return false;
    }

    public addView(viewName: string, container: HTMLElement) {
        const view: ViewMetadata = {
            name: viewName,
            componentGuid: "",
            isMain: false,
            placeholder: container,
            root: null!,
            head: null!,
            scriptsLoadTasks: new Map<string, Task<void>>(),
            pluginsLoadTask: new Task<void>(),
            modules: new Map<string, any>(),
            nativeObjectNames: [],
        };
        this.views.push(view);
        
        this.forceUpdate();
    }

    public removeView(viewName: string) {
        /*const viewIndex = this.views.findIndex(v => this.views)
        this.views.splice(viewIndex, 1);*/
        this.forceUpdate();
    }

    public render(): React.ReactNode {
        return this.views.map(v => <ViewPortal key={v.name} name={v.name} component={null!/*v.componentGuid*/} placeholder={v.placeholder} />);
    }
}

export function createView(componentClass: any, properties: {}, view: ViewMetadata, componentName: string) {
    const viewProtalsCollectionRef = React.createRef<ViewPortalsCollection>();
    const viewContextInstance = new ViewContext(view.name, viewProtalsCollectionRef.current!);

    componentClass.contextType = pluginsContext;

    return (
        React.createElement(viewContext.Provider, { value: viewContextInstance },
            React.createElement(pluginsContext.Provider, { value: new PluginsContext(Array.from(view.modules.values())) },
                <>
                    <ViewPortalsCollection ref={viewProtalsCollectionRef}/>,
                    {React.createElement(componentClass, { ref: e => view.modules.set(componentName, e), ...properties })}
                </>
            )
        )
    );
}

export function renderMainView(children: React.ReactElement, container: Element): Promise<void> {
    return new Promise<void>(resolve => ReactDOM.hydrate(children, container, resolve));
}