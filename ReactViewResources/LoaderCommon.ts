/// <reference path="Dictionary.d.ts"/>

type Listener = (viewName: string) => void;

const views: Dictionary<ViewData> = {};
const viewAddListeners: Listener[] = [];
const viewRemoveListeners: Listener[] = [];

export const webViewRootId = "webview_root";

export type ViewData = { root: HTMLElement, stylesheetsContainer: HTMLElement };
export interface Type<T> extends Function { new(...args: any[]): T; }

export class Task<ResultType> {

    private taskPromise: Promise<ResultType>;
    private resolve: (result: ResultType) => void;

    constructor() {
        this.taskPromise = new Promise<ResultType>((resolve) => this.resolve = resolve);
    }

    public setResult(result?: ResultType) {
        this.resolve(result as ResultType);
    }

    public get promise() {
        return this.taskPromise;
    }
}

export class PluginsContext {

    private pluginInstances: Dictionary<any> = {};

    constructor(plugins: any[]) {
        this.pluginInstances = {};
        plugins.forEach(p => this.pluginInstances[p.constructor.name] = p);
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances[_class.name];
    }
}

export function addViewElement(viewName: string, root: HTMLElement, stylesheetsContainer: HTMLElement): void {
    if (views[viewName]) {
        throw new Error(`A view with the name "${viewName}" has already been created`);
    }
    views[viewName] = { root, stylesheetsContainer }
    viewAddListeners.forEach(l => l(viewName));
}

export function removeViewElement(viewName: string): void {
    delete views[viewName];
    viewRemoveListeners.forEach(l => l(viewName));
}

export function getViewElement(viewName: string): ViewData {
    return views[viewName];
}

export function addViewAddedEventListener(listener: Listener) {
    viewAddListeners.push(listener);
}

export function addViewRemovedEventListener(listener: Listener) {
    viewRemoveListeners.push(listener);
}

export function getStylesheets(stylesheetsContainer: HTMLElement): HTMLLinkElement[] {
    return Array.from(stylesheetsContainer.getElementsByTagName("link"));
}