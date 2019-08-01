import * as Common from "./LoaderCommon";

class ViewFrame extends HTMLElement {

    private headElement: HTMLElement;
    private rootElement: HTMLElement;

    constructor() {
        super();
        let root = this.attachShadow({ mode: "closed" });

        // get sticky stylesheets
        let mainView = Common.getViewElement("");
        let stylesheets = Common.getStylesheets(mainView.stylesheetsContainer).filter(s => s.dataset.sticky === "true");

        this.headElement = document.createElement("head");

        // reset inherited css properties
        let style = document.createElement("style");
        style.textContent = ":host { all: initial; display: block; }";
        this.headElement.appendChild(style);

        // import sticky stylesheets into this view
        stylesheets.forEach(s => this.headElement.appendChild(document.importNode(s, false)));
        root.appendChild(this.headElement);

        // create root container
        this.rootElement = document.createElement("div");
        this.rootElement.id = Common.WebViewRootId;

        let body = document.createElement("body");
        body.appendChild(this.rootElement);

        root.appendChild(body);
    }

    public connectedCallback() {
        Common.addViewElement(this.id, this.rootElement, this.headElement);
    }

    public disconnectedCallback() {
        Common.removeViewElement(this.id);
    }
}

customElements.define("view-frame", ViewFrame);