import * as Loader from "./Loader.js";

class ViewFrame extends HTMLElement {

    private headElement: HTMLElement;
    private rootElement: HTMLElement;

    constructor() {
        super();
        let root = this.attachShadow({ mode: "closed"  });

        this.headElement = document.createElement("head");
        root.appendChild(this.headElement);

        let style = document.createElement("style");
        style.textContent = ":host { all: initial; display: block; background: white; }"; // reset inherited css properties
        this.headElement.appendChild(style);

        this.rootElement = document.createElement("div");
        this.rootElement.id = "webview_root";

        let body = document.createElement("body");
        body.appendChild(this.rootElement);

        root.appendChild(body);
    }

    public connectedCallback() {
        Loader.addViewElement(this.id, this.rootElement, this.headElement);
    }

    public disconnectedCallback() {
        Loader.removeViewElement(this.id);
    }
}

customElements.define("view-frame", ViewFrame);