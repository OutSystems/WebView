type Listener = (view: View) => void;

const views: Map<string, View> = new Map();
const viewAddListeners: Listener[] = [];
const viewRemoveListeners: Listener[] = [];

export const mainFrameName = "";
export const webViewRootId = "webview_root";

export type View = {
    name: string,
    root: HTMLElement,
    head: HTMLElement,
    scriptsLoadTasks: Map<string, Task<void>>; // maps scripts urls to load tasks
    pluginsLoadTask: Task<void>; // plugins load task
    modules: Map<string, any>; // maps module name to module instance
    nativeObjectNames: string[]; // list of frame native objects
};

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

    private pluginInstances: Map<string, any> = new Map<string, any>();

    constructor(plugins: any[]) {
        plugins.forEach(p => this.pluginInstances.set(p.constructor.name, p));
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances.get(_class.name);
    }
}

export function addView(viewName: string, root: HTMLElement, head: HTMLElement): void {
    if (views.has(viewName)) {
        throw new Error(`A view with the name "${viewName}" has already been created`);
    }
    const view: View = {
        name: viewName,
        root: root,
        head: head,
        scriptsLoadTasks: new Map<string, Task<void>>(),
        pluginsLoadTask: new Task<void>(),
        modules: new Map<string, any>(),
        nativeObjectNames: []
    };
    views.set(viewName, view);
    viewAddListeners.forEach(l => l(view));
}

export function removeView(viewName: string): void {
    const view = views.get(viewName);
    if (view) {
        views.delete(viewName);
        viewRemoveListeners.forEach(l => l(view));
    }
}

export function getView(viewName: string): View {
    return views.get(viewName) as View;
}

export function addViewAddedEventListener(listener: Listener) {
    viewAddListeners.push(listener);
}

export function addViewRemovedEventListener(listener: Listener) {
    viewRemoveListeners.push(listener);
}

export function getStylesheets(head: HTMLElement): HTMLLinkElement[] {
    return Array.from(head.getElementsByTagName("link"));
}