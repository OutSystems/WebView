import * as Loader from "./Loader.js";

export class ViewFrame extends HTMLElement {

    private shadowRootElement : ShadowRoot;

    constructor() {
        super();
        this.shadowRootElement = this.attachShadow({ mode: "open" });
    }

    public connectedCallback() {
        Loader.addViewElement(this.id, this.shadowRootElement.getRootNode() as HTMLElement);
    }

    public disconnectedCallback() {
        Loader.removeViewElement(this.id);
    }
}

customElements.define("view-frame", ViewFrame);