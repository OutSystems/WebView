export type ViewData = { root: HTMLElement, stylesheetsContainer: HTMLElement };

type Listener = (viewName: string) => void;

export const WebViewRootId = "webview_root";

const Views: { [name: string]: ViewData } = {};
const Listeners: Listener[] = [];

export function addViewElement(viewName: string, root: HTMLElement, stylesheetsContainer: HTMLElement): void {
    Views[viewName] = { root, stylesheetsContainer }
    Listeners.forEach(l => l(viewName));
}

export function removeViewElement(viewName: string): void {
    delete Views[viewName];
}

export function getViewElement(viewName: string): ViewData {
    return Views[viewName];
}

export function addViewAddedEventListener(listener: Listener) {
    Listeners.push(listener);
}

export function getStylesheets(stylesheetsContainer: HTMLElement): HTMLLinkElement[] {
    return Array.from(stylesheetsContainer.getElementsByTagName("link"));
}