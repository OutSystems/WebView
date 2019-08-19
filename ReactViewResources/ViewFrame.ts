import * as Common from "./LoaderCommon";

class ViewFrame extends HTMLElement {

    private headElement: HTMLElement;
    private rootElement: HTMLElement;

    public connectedCallback() {
        if (this.id === "") {
            throw new Error("View Frame Id must be specified (not empty)");
        }

        const root = this.attachShadow({ mode: "closed" });
        
        // get sticky stylesheets
        const mainView = Common.getView(Common.mainFrameName);
        const stylesheets = Common.getStylesheets(mainView.head).filter(s => s.dataset.sticky === "true");

        this.headElement = document.createElement("head");

        // reset inherited css properties
        const style = document.createElement("style");
        style.textContent = ":host { all: initial; display: block; }";
        this.headElement.appendChild(style);

        // import sticky stylesheets into this view
        stylesheets.forEach(s => this.headElement.appendChild(document.importNode(s, false)));
        root.appendChild(this.headElement);

        // create root container
        this.rootElement = document.createElement("div");
        this.rootElement.id = Common.webViewRootId;

        const body = document.createElement("body");
        body.appendChild(this.rootElement);

        root.appendChild(body);

        Common.addView(this.id, this.rootElement, this.headElement);
    }

    public disconnectedCallback() {
        Common.removeView(this.id);
    }
}

customElements.define("view-frame", ViewFrame);