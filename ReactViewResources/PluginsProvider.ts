const plugins: { [frameName: string]: { plugin: PluginConstructor, moduleFullName: string, nativeObject: any }[] } = {};

export interface Type<T> extends Function { new(...args: any[]): T; }

interface PluginConstructor {
    new(nativeObject: any): Plugin;
}

interface Plugin { }

export function registerPlugin(frameName: string, plugin: PluginConstructor, moduleFullName: string, nativeObject: any) {
    let framePlugins = plugins[frameName];
    if (!framePlugins) {
        framePlugins = [];
        plugins[frameName] = framePlugins;
    }
    framePlugins.push({ plugin, moduleFullName, nativeObject });
}

export function unRegisterPlugins(frameName: string) {
    delete plugins[frameName];
}

export class PluginsContext {

    private pluginInstances: Dictionary<Plugin> = {};

    constructor(frameName: string, modulesRegistry: Dictionary<any>) {
        const framePlugins = plugins[frameName] || [];
        framePlugins.forEach(p => {
            const pluginInstance = new p.plugin(p.nativeObject);
            modulesRegistry[p.moduleFullName] = pluginInstance; 
            this.pluginInstances[p.plugin.name] = pluginInstance;
        });
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances[_class.name];
    }
}
