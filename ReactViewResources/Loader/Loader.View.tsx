import * as React from "react";
import * as ReactDOM from "react-dom";
import { PluginsContext, PluginsContextHolder } from "./PluginsContext";
import { ViewContext } from "./ViewContext";
import { ViewMetadata } from "./ViewMetadata";
import { ViewPortalsCollection } from "./ViewPortalsCollection";

export function createView(componentClass: any, properties: {}, view: ViewMetadata, componentName: string, viewAddedHandler: (view: ViewMetadata) => void) {
    componentClass.contextType = PluginsContext;

    return (
        <ViewContext.Provider value={view}>
            <PluginsContext.Provider value={new PluginsContextHolder(Array.from(view.modules.values()))}>
                <ViewPortalsCollection views={view.childViews} viewAddedHandler={viewAddedHandler} />
                {React.createElement(componentClass, { ref: e => view.modules.set(componentName, e), ...properties })}
            </PluginsContext.Provider>
        </ViewContext.Provider>
    );
}

export function renderMainView(children: React.ReactElement, container: Element): Promise<void> {
    return new Promise<void>(resolve => ReactDOM.hydrate(children, container, resolve));
}