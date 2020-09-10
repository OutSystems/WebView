namespace AMDLoader {
    export const defines: { [module: string]: boolean } = {};
    const promises: { [module: string]: Promise<any> } = {};
    const resolves: { [module: string]: Function } = {};

    export function getOrCreateDependencyPromise(name: string) {
        name = name.replace(/^.\//, "");
        if (!promises[name]) {
            promises[name] = new Promise((resolve) => resolves[name] = resolve);
        }
        return promises[name];
    }

    export function resolve(name: string, value: any) {
        getOrCreateDependencyPromise(name); // create promise if necessary
        resolves[name](value);
        defines[name] = true;
    }

    export function require(deps: string[], definition: Function) {
        if (!deps || deps.length === 0) {
            definition.apply(null, []);
            return;
        }
        const promises = deps.map(AMDLoader.getOrCreateDependencyPromise);
        Promise.all(promises).then((result) => {
            if (definition) {
                definition.apply(null, result);
            }
        });
    }
}

function define(name: string, deps: string[], definition: Function) {
    if (name in AMDLoader.defines) {
        throw new Error("Module " + name + " already defined");
    }
    AMDLoader.require(deps, function () {
        const exportsIdx = deps.indexOf("exports");
        if (exportsIdx >= 0) {
            // create a brand new export object to store the module exports
            arguments[exportsIdx] = {};
        }
        const moduleExports = definition.apply(null, arguments) || arguments[exportsIdx];
        AMDLoader.resolve(name, moduleExports);
        exports[name] = moduleExports;
    });
}

define("require", [], function () { return AMDLoader.require; });
define("exports", [], function () { return {}; });
