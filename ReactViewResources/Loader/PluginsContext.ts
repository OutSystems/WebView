import * as React from "react";
import { IPluginsContext } from "PluginsProvider";

export interface Type<T> extends Function { new(...args: any[]): T; }

export class PluginsContextHolder implements IPluginsContext {

    private pluginInstances: Map<string, any> = new Map<string, any>();

    constructor(plugins: any[]) {
        plugins.forEach(p => this.pluginInstances.set(p.constructor.name, p));
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances.get(_class.name);
    }
}

export const PluginsContext = React.createContext<PluginsContextHolder>(null!);

window["PluginsProvider"] = { PluginsContext: PluginsContext };