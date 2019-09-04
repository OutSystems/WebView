export const mainFrameName = "";
export const webViewRootId = "webview_root";

export function getStylesheets(head: Element): HTMLLinkElement[] {
    return Array.from(head.getElementsByTagName("link"));
}