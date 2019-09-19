import * as React from "react";
import * as ReactDOM from "react-dom";
import { PluginsContext, PluginsContextHolder } from "./PluginsContext";
import { ViewContext } from "./ViewContext";
import { ViewMetadata } from "./ViewMetadata";
import { ViewPortalsCollection, ViewLifecycleEventHandler } from "./ViewPortalsCollection";
import { ResourceLoader } from "./ResourceLoader";
import { ResourceLoaderUrlFormatter } from "ResourceLoader";

export function createView(
    componentClass: any,
    properties: {},
    view: ViewMetadata,
    componentName: string,
    childViewAddedHandler: ViewLifecycleEventHandler,
    childViewRemovedHandler: ViewLifecycleEventHandler,
    customResourceBaseUrl: string) {

    componentClass.contextType = PluginsContext;

    const makeResourceUrl: ResourceLoaderUrlFormatter = (resourceKey: string, ...params: string[]) => {
        const urlTail = [resourceKey].concat(params).map(p => encodeURIComponent(p)).join("&");
        return `${customResourceBaseUrl}/${view.name}/?${urlTail}`;
    }

    return (
        <ViewContext.Provider value={view}>
            <PluginsContext.Provider value={new PluginsContextHolder(Array.from(view.modules.values()))}>
                <ResourceLoader.Provider value={makeResourceUrl}>
                    <ViewPortalsCollection views={view.childViews} viewAdded={childViewAddedHandler} viewRemoved={childViewRemovedHandler} />
                    {React.createElement(componentClass, { ref: e => view.modules.set(componentName, e), ...properties })}
                </ResourceLoader.Provider>
            </PluginsContext.Provider>
        </ViewContext.Provider>
    );
}

export function renderMainView(children: React.ReactElement, container: Element): Promise<void> {
    return new Promise<void>(resolve => ReactDOM.hydrate(children, container, resolve));
}