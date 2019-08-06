export interface IViewPluginProperties {
    notifyViewLoaded(viewName: string): void;
}

export interface IViewPluginPropertiesBehaviors {
    test(): void;
}

console.log("Plugin loaded");

export default class ViewPlugin implements IViewPluginPropertiesBehaviors {

    constructor(private nativeObject: IViewPluginProperties) {
    }

    public notifyViewLoaded(viewName: string): void {
        this.nativeObject.notifyViewLoaded(viewName);
    }

    public test(): void {
        alert("test called");
    }
}