export interface IViewPluginProperties {
    notifyViewLoaded(viewName: string): void;
}

console.log("Plugin loaded");

declare const ViewPlugin: IViewPluginProperties;

export function notifyViewLoaded(viewName: string) {
    ViewPlugin.notifyViewLoaded(viewName);
}