export interface Type<T> extends Function { new(...args: any[]): T; }

export class PluginsContext {

    private pluginInstances: Map<string, any> = new Map<string, any>();

    constructor(plugins: any[]) {
        plugins.forEach(p => this.pluginInstances.set(p.constructor.name, p));
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances.get(_class.name);
    }
}