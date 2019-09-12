import * as React from "react";
import { ResourceLoaderUrlFormatter } from "./../../ViewGenerator/contentFiles/node_modules/@types/ResourceLoader";

export const ResourceLoader = React.createContext<ResourceLoaderUrlFormatter>(() => "");

window["ResourceLoader"] = { ResourceLoader: ResourceLoader };