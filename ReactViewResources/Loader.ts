declare const CefSharp: {
    BindObjectAsync(objName1: string, objName2: string): Promise<void>
};

type Dictionary<T> = { [key: string]: T };

const ReactLib: string = "React";
const ReactDOMLib: string = "ReactDOM";
const ModuleLib: string = "Bundle";
const JsExtension: string = ".js";

class Task<ResultType> {

    private taskPromise: Promise<ResultType>;
    private resolve: (result: ResultType) => void;

    constructor() {
        this.taskPromise = new Promise<ResultType>((resolve) => this.resolve = resolve);
    }

    public setResult(result?: ResultType) {
        this.resolve(result as ResultType);
    }

    public get promise() {
        return this.taskPromise;
    }
}

const [LibsPath, EnableDebugMode, ModulesObjectName, EventListenerObjectName, ReadyEventName] = Array.from(new URLSearchParams(location.search).keys());

const ExternalLibsPath = LibsPath + "node_modules/";

const Modules: Dictionary<{}> = {};
window[ModulesObjectName] = Modules;

const StylesheetsLoadTask = new Task();
const PluginsLoadTask = new Task();
const BootstrapTask = new Task();

export async function showErrorMessage(msg: string): Promise<void> {
    const ContainerId = "webview_error";
    let msgContainer = document.getElementById(ContainerId) as HTMLDivElement;
    if (!msgContainer) {
        msgContainer = document.createElement("div");
        msgContainer.id = ContainerId;
        let style = msgContainer.style;
        style.backgroundColor = "#f45642";
        style.color = "white";
        style.fontFamily = "Arial";
        style.fontWeight = "bold";
        style.fontSize = "10px"
        style.padding = "3px";
        style.position = "absolute";
        style.top = "0";
        style.left = "0";
        style.right = "0";
        style.zIndex = "10000";
        style.height = "auto";
        style.wordWrap = "break-word";

        await waitForDOMReady();
        document.body.appendChild(msgContainer);   
    }
    msgContainer.innerText = msg;
}

function loadScript(scriptSrc: string): Promise<void> {
    return new Promise((resolve) => {
        // load react view resources
        let script = document.createElement("script");
        script.src = scriptSrc;
        script.addEventListener("load", () => resolve());
        let head = document.head;
        if (!head) {
            throw new Error("Document not ready");
        }
        head.appendChild(script);
    });
}

async function loadFramework(): Promise<void> {
    let frameworkPromises: Promise<void>[] = [];
    frameworkPromises.push(
        /* React */ loadScript(ExternalLibsPath + "react/umd/react.production.min.js"),
        /* ReactDOM */ loadScript(ExternalLibsPath + "react-dom/umd/react-dom.production.min.js"),
        /* Prop-Types */ loadScript(ExternalLibsPath + "prop-types/prop-types.min.js")
    );
    await Promise.all(frameworkPromises);

    /* React View Resources */ await loadScript(LibsPath + "ReactViewResources.js");
}

export function loadStyleSheet(stylesheet: string): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;

            // TODO mal
            // await new Promise((resolve) => require(["css!" + stylesheet], resolve));

            if (document.head) {
                // mark default stylesheet as sticky to prevent it from being removed and added again later
                let linkElement = getAllStylesheets().find(l => l.href === stylesheet);
                if (linkElement) {
                    linkElement.dataset.sticky = "true";
                }
            }

            StylesheetsLoadTask.setResult();
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

export function loadPlugins(plugins: string[][]): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;

            if (plugins && plugins.length > 0) {
                // load plugin modules
                let pluginsPromises: Promise<void>[] = [];
                plugins.forEach(m => {
                    const ModuleName: string = m[0];
                    const NativeObjectName: string = m[1];
                    const MainModule: string = m[2];

                    let externalSources: string[] = m.length > 2 ? m.slice(3) : [];

                    pluginsPromises.push(new Promise<void>((resolve, reject) => {
                        CefSharp.BindObjectAsync(NativeObjectName, NativeObjectName).then(async () => {
                            if (ModuleName) {

                                // External sources
                                let loadExternalSourcesPromises: Promise<void>[] = [];
                                externalSources.forEach(s => {
                                    loadExternalSourcesPromises.push(loadScript(s));
                                });
                                await Promise.all(loadExternalSourcesPromises);

                                // Main module
                                loadScript(MainModule).then(() => resolve());

                            } else {
                                reject(`Failed to load '${ModuleName}' (might not be a module)`);
                            }
                        });
                    }));
                });
                await Promise.all(pluginsPromises);
            }

            PluginsLoadTask.setResult();
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

export function loadComponent(
    componentName: string,
    componentSource: string,
    componentNativeObjectName: string,
    maxPreRenderedCacheEntries: number,
    hasStyleSheet: boolean,
    hasPlugins: boolean,
    componentNativeObject: Dictionary<any>,
    componentHash: string,
    componentSourceURL: string,
    originalSourceFolder: string,
    componentExternalSources: string[]): void {

    function getComponentCacheKey(propertiesHash: string) {
        return componentSource + "|" + propertiesHash;
    }

    async function innerLoad() {
        try {
            // force images and other resources load from the appropriate path
            (document.getElementById("webview_base") as HTMLBaseElement).href = originalSourceFolder;

            const RootElement = document.getElementById("webview_root") as HTMLDivElement;

            if (hasStyleSheet) {
                // wait for the stylesheet to load before first render
                await StylesheetsLoadTask.promise;
            }

            const ComponentCacheKey = getComponentCacheKey(componentHash);
            const CachedElementHtml = localStorage.getItem(ComponentCacheKey);
            if (CachedElementHtml) {
                // render cached component html to reduce time to first render
                RootElement.innerHTML = CachedElementHtml;
                await waitForNextPaint();
            }

            let promisesToWaitFor = [BootstrapTask.promise];
            if (hasPlugins) {
                promisesToWaitFor.push(PluginsLoadTask.promise);
            }
            await Promise.all(promisesToWaitFor);

            // load component module external sources
            let loadExternalSourcesPromises: Promise<void>[] = [];
            componentExternalSources.forEach(s => {
                loadExternalSourcesPromises.push(loadScript(s));            
            });
            await Promise.all(loadExternalSourcesPromises);

            // main component script should be the last to be loaded, otherwise errors might occur
            await loadScript(componentSourceURL);

            const Component = window[ModuleLib].default;
            const React = window[ReactLib];
            const ReactDOM = window[ReactDOMLib];

            // create proxy for properties obj to delay its methods execution until native object is ready
            const Properties = createPropertiesProxy(componentNativeObject, componentNativeObjectName);

            // render component
            await new Promise((resolve) => {
                const Root = React.createElement(Component, Properties);
                Modules[componentName] = ReactDOM.hydrate(Root, RootElement, resolve);
            });

            await waitForNextPaint();

            if (!CachedElementHtml && maxPreRenderedCacheEntries > 0) {
                // cache view html for further use
                const ElementHtml = RootElement.innerHTML;
                // get all stylesheets except the stick ones (which will be loaded by the time the html gets rendered) otherwise we could be loading them twice
                const Stylesheets = getAllStylesheets().filter(l => l.dataset.sticky !== "true").map(l => l.outerHTML).join("");

                localStorage.setItem(ComponentCacheKey, Stylesheets + ElementHtml); // insert html into the cache

                let componentCachedInfo = localStorage.getItem(componentSource);
                let cachedEntries: string[] = componentCachedInfo ? JSON.parse(componentCachedInfo) : [];

                // remove cached entries that are older tomantina cache size within limits
                while (cachedEntries.length >= maxPreRenderedCacheEntries) {
                    let olderCacheEntryKey = cachedEntries.shift() as string;
                    localStorage.removeItem(getComponentCacheKey(olderCacheEntryKey));
                }

                cachedEntries.push(componentHash);
                localStorage.setItem(componentSource, JSON.stringify(cachedEntries));
            }

            window.dispatchEvent(new Event('viewready'));
            window[EventListenerObjectName].notify(ReadyEventName, window.name);
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

async function bootstrap() {
    // prevent browser from loading the dropped file
    window.addEventListener("dragover", (e) => e.preventDefault());
    window.addEventListener("drop", (e) => e.preventDefault());

    await waitForDOMReady();
    await loadFramework();

    // bind event listener object ahead-of-time
    await CefSharp.BindObjectAsync(EventListenerObjectName, EventListenerObjectName);

    BootstrapTask.setResult();
}

function createPropertiesProxy(basePropertiesObj: {}, nativeObjName: string): {} {
    let proxy = Object.assign({}, basePropertiesObj);
    Object.keys(proxy).forEach(key => {
        let value = basePropertiesObj[key];
        if (value !== undefined) {
            proxy[key] = value;
        } else {
            proxy[key] = async function () {
                let nativeObject = window[nativeObjName];
                if (!nativeObject) {
                    await new Promise(async (resolve) => {
                        await waitForNextPaint();
                        await CefSharp.BindObjectAsync(nativeObjName, nativeObjName);
                        nativeObject = window[nativeObjName];
                        resolve();
                    });
                }
                return nativeObject[key].apply(window, arguments);
            };
        }
    });
    return proxy;
}

function handleError(error: Error) {
    if (EnableDebugMode) {
        showErrorMessage(error.message);
    }
    throw error;
}

function waitForNextPaint() {
    return new Promise((resolve) => {
        requestAnimationFrame(() => {
            setTimeout(resolve);
        });
    });
}

function getAllStylesheets(): HTMLLinkElement[] {
    return document.head ? Array.from(document.head.getElementsByTagName("link")) : [];
}

function waitForDOMReady() {
    if (document.readyState === "loading") {
        return new Promise((resolve) => document.addEventListener("DOMContentLoaded", resolve, { once: true }));
    }
    return Promise.resolve();
}

bootstrap();
