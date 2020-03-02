import * as React from "react";
import { ResourceLoaderUrlFormatter } from "ResourceLoader";

let customResourceBaseUrl = "";
export function setCustomResourceBaseUrl(url: string): void {
    customResourceBaseUrl = url;
}

export function formatUrl(viewName: string, resourceKey: string, ...params: string[]): string {
    const urlTail = [resourceKey].concat(params).map(p => encodeURIComponent(p)).join("&");
    return `${customResourceBaseUrl}/${viewName}/?${urlTail}`;
}

export const ResourceLoader = React.createContext<ResourceLoaderUrlFormatter>(() => "");

window["ResourceLoader"] = { ResourceLoader: ResourceLoader };