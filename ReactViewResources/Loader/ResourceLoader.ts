import * as React from "react";
import { ResourceLoaderUrlFormatter } from "ResourceLoader";

export const ResourceLoader = React.createContext<ResourceLoaderUrlFormatter>(() => "");

window["ResourceLoader"] = { ResourceLoader: ResourceLoader };