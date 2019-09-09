import * as React from "react";

type ResourceLoaderUrlFormatter = (resourceKey: string) => string;

export const ResourceLoader = React.createContext<ResourceLoaderUrlFormatter>(() => "");

window["ResourceLoader"] = { ResourceLoader: ResourceLoader };