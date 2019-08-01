/// <reference path="Dictionary.d.ts"/>

export type ViewData = { root: HTMLElement, stylesheetsContainer: HTMLElement };

type Listener = (viewName: string) => void;

export const WebViewRootId = "webview_root";

const Views: Dictionary<ViewData> = {};
const AddListeners: Listener[] = [];
const RemoveListeners: Listener[] = [];

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

export function addViewElement(viewName: string, root: HTMLElement, stylesheetsContainer: HTMLElement): void {
    Views[viewName] = { root, stylesheetsContainer }
    AddListeners.forEach(l => l(viewName));
}

export function removeViewElement(viewName: string): void {
    delete Views[viewName];
    RemoveListeners.forEach(l => l(viewName));
}

export function getViewElement(viewName: string): ViewData {
    return Views[viewName];
}

export function addViewAddedEventListener(listener: Listener) {
    AddListeners.push(listener);
}

export function addViewRemovedEventListener(listener: Listener) {
    RemoveListeners.push(listener);
}

export function getStylesheets(stylesheetsContainer: HTMLElement): HTMLLinkElement[] {
    return Array.from(stylesheetsContainer.getElementsByTagName("link"));
}