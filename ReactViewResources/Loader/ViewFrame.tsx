import * as React from "react";
import * as ReactDOM from "react-dom";
import * as Common from "./LoaderCommon";

type ViewFrameProps = { name: string, className: string };

class ViewFrame extends React.Component<ViewFrameProps> {

    private shadowRoot: Element | null;
    private head: HTMLElement | null;
    private root: HTMLElement | null;
    private children: React.ReactNode;

    constructor(props: ViewFrameProps, context: any) {
        super(props, context);
        if (props.name === "") {
            throw new Error("View Frame name must be specified (not empty)");
        }
    }

    public shouldComponentUpdate(): boolean {
        // prevent component updates, it will be updated explicitly
        return false;
    }

    public componentWillUnmount() {
        Common.removeView(this.props.name);
    }

    private renderChildren = (children: React.ReactNode) => {
        return new Promise<void>(resolve => {
            this.children = children;
            this.forceUpdate(resolve);
        });
    }

    private setContainer(container: HTMLElement | null) {
        if (container && !this.shadowRoot) {
            this.shadowRoot = container.attachShadow({ mode: "closed" }).getRootNode() as Element;
            this.forceUpdate(() => {
                if (!this.root || !this.head) {
                    throw new Error("Expected root and head to be set");
                }
                Common.addView(this.props.name, false, this.root, this.head, this.renderChildren);
            });
        }
    }

    private renderPortal() {
        if (!this.shadowRoot) {
            return null;
        }

        // get sticky stylesheets
        const mainView = Common.getView(Common.mainFrameName);
        const stylesheets = Common.getStylesheets(mainView.head).filter(s => s.dataset.sticky === "true");

        const headContent =
            "<style>:host { all: initial; display: block; }</style>\n" + // reset inherited css properties
            stylesheets.map(s => s.outerHTML).join("\n"); // import sticky stylesheets into this view 

        return ReactDOM.createPortal(
            <>
                <head ref={e => this.head = e} dangerouslySetInnerHTML={{ __html: headContent }} />
                <body>
                    <div ref={e => this.root = e} id={Common.webViewRootId}>
                        {this.children}
                    </div>
                </body>
            </>,
            this.shadowRoot);
    }

    public render() {
        return (
            <div ref={e => this.setContainer(e)} className={this.props.className}>
                {this.renderPortal()}
            </div>
        );
    }
}

window["ViewFrame"] = { ViewFrame: ViewFrame };
