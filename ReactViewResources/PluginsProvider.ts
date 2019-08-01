const Plugins: { [frameName: string]: { plugin: PluginConstructor, moduleFullName: string, nativeObject: any }[] } = {};

export interface Type<T> extends Function { new(...args: any[]): T; }

interface PluginConstructor {
    new(nativeObject: any): Plugin;
}

interface Plugin { }

export function registerPlugin(frameName: string, plugin: PluginConstructor, moduleFullName: string, nativeObject: any) {
    let framePlugins = Plugins[frameName];
    if (!framePlugins) {
        framePlugins = [];
        Plugins[frameName] = framePlugins;
    }
    framePlugins.push({ plugin, moduleFullName, nativeObject });
}

export function unRegisterPlugins(frameName: string) {
    delete Plugins[frameName];
}

export class PluginsContext {

    private pluginInstances: Dictionary<Plugin> = {};

    constructor(frameName: string, modulesRegistry: Dictionary<any>) {
        const FramePlugins = Plugins[frameName] || [];
        FramePlugins.forEach(p => {
            const PluginInstance = new p.plugin(p.nativeObject);
            modulesRegistry[p.moduleFullName] = PluginInstance; 
            this.pluginInstances[p.plugin.name] = PluginInstance;
        });
    }

    public getPluginInstance<T>(_class: Type<T>) {
        return this.pluginInstances[_class.name];
    }
}
