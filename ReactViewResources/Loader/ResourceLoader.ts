import * as React from "react";
import { ResourceLoaderUrlFormatter } from "ResourceLoader";

export function createResourceLoaderUrlFormatter(customResourceBaseUrl: string, viewName: string): ResourceLoaderUrlFormatter {
    return (resourceKey: string, ...params: string[]) => {
        const urlTail = [resourceKey].concat(params).map(p => encodeURIComponent(p)).join("&");
        return `${customResourceBaseUrl}/${viewName}/?${urlTail}`;
    };
}

export const ResourceLoader = React.createContext<ResourceLoaderUrlFormatter>(() => "");

window["ResourceLoader"] = { ResourceLoader: ResourceLoader };