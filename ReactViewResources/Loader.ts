import * as Common from "./LoaderCommon";
import { Task, PluginsContext } from "./LoaderCommon";
import "./ViewFrame";

declare const CefSharp: {
    BindObjectAsync(settings: { NotifyIfAlreadyBound?: boolean, IgnoreCache: boolean }, objName: string): Promise<void>
    DeleteBoundObject(objName: string): boolean;
};

const mainFrameName = "";
const reactLib: string = "React";
const reactDOMLib: string = "ReactDOM";
const viewsBundleName: string = "Views";
const pluginsBundleName: string = "Plugins";

const [
    libsPath,
    enableDebugMode,
    modulesObjectName,
    eventListenerObjectName,
    viewInitializedEventName,
    viewDestroyedEventName,
    viewLoadedEventName
] = Array.from(new URLSearchParams(location.search).keys());

const externalLibsPath = libsPath + "node_modules/";

const modules: Dictionary<Dictionary<any>> = (() => window[modulesObjectName] = {})();
const nativeObjectNames: Dictionary<string[]> = {};

const bootstrapTask = new Task();
const stylesheetsLoadTask = new Task();
const pluginsLoadTasks: Dictionary<Task<any>> = {};

let rootContext: React.Context<string>;

export async function showErrorMessage(msg: string): Promise<void> {
    const containerId = "webview_error";
    let msgContainer = document.getElementById(containerId) as HTMLDivElement;
    if (!msgContainer) {
        msgContainer = document.createElement("div");
        msgContainer.id = containerId;
        const style = msgContainer.style;
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
        const script = document.createElement("script");
        script.src = scriptSrc;
        script.addEventListener("load", () => resolve());
        const head = document.head;
        if (!head) {
            throw new Error("Document not ready");
        }
        head.appendChild(script);
    });
}

function loadStyleSheet(stylesheet: string, containerElement: HTMLElement, markAsSticky: boolean): Promise<void> {
    return new Promise((resolve) => {
        const link = document.createElement("link");
        link.type = "text/css";
        link.rel = "stylesheet";
        link.href = stylesheet;
        link.addEventListener("load", () => resolve());
        if (markAsSticky) {
            link.dataset.sticky = "true";
        }
        containerElement.appendChild(link);
    });
}

async function loadFramework(): Promise<void> {
    await loadScript(externalLibsPath + "prop-types/prop-types.min.js"); /* Prop-Types */
    await loadScript(externalLibsPath + "react/umd/react.production.min.js"); /* React */ 
    await loadScript(externalLibsPath + "react-dom/umd/react-dom.production.min.js"); /* ReactDOM */
}

export function loadDefaultStyleSheet(stylesheet: string): void {
    async function innerLoad() {
        try {
            await bootstrapTask.promise;
            await loadStyleSheet(stylesheet, document.head, true);

            stylesheetsLoadTask.setResult();
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

export function loadPlugins(plugins: any[][], frameName: string, forMainFrame: boolean): void {
    async function innerLoad() {
        try {
            const loadTask = new Task();
            pluginsLoadTasks[frameName] = loadTask;

            await bootstrapTask.promise;

            if (!forMainFrame) {
                // wait for main frame plugins to be loaded, otherwise modules won't be loaded yet
                await pluginsLoadTasks[mainFrameName].promise;
            }

            if (plugins && plugins.length > 0) {
                const pluginsInstances = (modules[frameName] = modules[frameName] || {});

                // load plugin modules
                const pluginsPromises = plugins.map(async m => {
                    const moduleName: string = m[0];
                    const mainJsSource: string = m[1];
                    const nativeObjectFullName: string = m[2]; // fullname with frame name included
                    const dependencySources: string[] = m[3];

                    if (forMainFrame) {
                        // only load plugins sources once (in the main frame)

                        // load plugin dependency js sources
                        const dependencySourcesPromises = dependencySources.map(s => loadScript(s));
                        await Promise.all(dependencySourcesPromises);

                        // plugin main js source
                        await loadScript(mainJsSource);
                    }

                    const pluginsBundle = window[pluginsBundleName];
                    const module = (pluginsBundle ? pluginsBundle[moduleName] : null) || window[viewsBundleName][moduleName];
                    if (!module || !module.default) {
                        throw new Error(`Failed to load '${moduleName}' (might not be a module with a default export)`);
                    }

                    const pluginNativeObject = await bindNativeObject(nativeObjectFullName, frameName);

                    pluginsInstances[moduleName] = new module.default(pluginNativeObject);
                });

                await Promise.all(pluginsPromises);
            }

            loadTask.setResult();
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

export function loadComponent(
    componentName: string,
    componentNativeObjectName: string,
    componentSource: string,
    dependencySources: string[],
    cssSources: string[],
    maxPreRenderedCacheEntries: number,
    hasStyleSheet: boolean,
    hasPlugins: boolean,
    componentNativeObject: Dictionary<any>,
    frameName: string,
    componentHash: string): void {

    function getComponentCacheKey(propertiesHash: string) {
        return componentSource + "|" + propertiesHash;
    }

    async function innerLoad() {
        try {
            const rootElementData = Common.getViewElement(frameName);
            const rootElement = rootElementData.root;

            if (hasStyleSheet) {
                // wait for the stylesheet to load before first render
                await stylesheetsLoadTask.promise;
            }

            const componentCacheKey = getComponentCacheKey(componentHash);
            const cachedElementHtml = localStorage.getItem(componentCacheKey);
            if (cachedElementHtml) {
                // render cached component html to reduce time to first render
                rootElement.innerHTML = cachedElementHtml;
                await waitForNextPaint();
            }

            const promisesToWaitFor = [bootstrapTask.promise];
            if (hasPlugins) {
                promisesToWaitFor.push(pluginsLoadTasks[frameName].promise);
            }
            await Promise.all(promisesToWaitFor);

            // load component dependencies js sources and css sources
            const dependencyLoadPromises =
                dependencySources.map(s => loadScript(s)).concat(
                    cssSources.map(s => loadStyleSheet(s, rootElementData.stylesheetsContainer, false)));
            await Promise.all(dependencyLoadPromises);

            // main component script should be the last to be loaded, otherwise errors might occur
            await loadScript(componentSource);

            const Component = window[viewsBundleName][componentName].default;
            const React = window[reactLib];
            const ReactDOM = window[reactDOMLib];

            // create proxy for properties obj to delay its methods execution until native object is ready
            const properties = createPropertiesProxy(componentNativeObject, componentNativeObjectName, frameName);

            // render component
            await new Promise((resolve) => {
                // create context
                if (!rootContext) {
                    rootContext = React.createContext(null);
                }
                Component.contextType = rootContext;

                const frameModules = modules[frameName] || {};
                const context = new PluginsContext(Object.values(frameModules));

                const rootRef = React.createRef();
                const root = React.createElement(rootContext.Provider, { value: context }, React.createElement(Component, { ref: rootRef, ...properties }));

                ReactDOM.hydrate(root, rootElement, () => {
                    modules[frameName] = Object.assign(modules[frameName] || {}, { [componentName]: rootRef.current });
                    resolve();
                });
            });

            await waitForNextPaint();

            if (!cachedElementHtml && maxPreRenderedCacheEntries > 0) {
                // cache view html for further use
                const elementHtml = rootElement.innerHTML;
                // get all stylesheets except the stick ones (which will be loaded by the time the html gets rendered) otherwise we could be loading them twice
                const stylesheets = Common.getStylesheets(rootElementData.stylesheetsContainer).filter(l => l.dataset.sticky !== "true").map(l => l.outerHTML).join("");

                localStorage.setItem(componentCacheKey, stylesheets + elementHtml); // insert html into the cache

                const componentCachedInfo = localStorage.getItem(componentSource);
                const cachedEntries: string[] = componentCachedInfo ? JSON.parse(componentCachedInfo) : [];

                // remove cached entries that are older tomantina cache size within limits
                while (cachedEntries.length >= maxPreRenderedCacheEntries) {
                    const olderCacheEntryKey = cachedEntries.shift() as string;
                    localStorage.removeItem(getComponentCacheKey(olderCacheEntryKey));
                }

                cachedEntries.push(componentHash);
                localStorage.setItem(componentSource, JSON.stringify(cachedEntries));
            }

            window.dispatchEvent(new Event('viewready'));

            fireNativeNotification(viewLoadedEventName, frameName);
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

    // add main view
    Common.addViewElement(mainFrameName, document.getElementById(Common.webViewRootId) as HTMLElement, document.head);

    Common.addViewAddedEventListener((frameName) => fireNativeNotification(viewInitializedEventName, frameName));
    Common.addViewRemovedEventListener((frameName) => {
        // delete native objects
        const nativeObjects = nativeObjectNames[frameName] || [];
        nativeObjects.forEach(nativeObjecName => CefSharp.DeleteBoundObject(nativeObjecName));

        delete modules[frameName]; // delete registered frame modules
        delete nativeObjectNames[frameName]; // delete registered native objects
        delete pluginsLoadTasks[frameName]; // delete load tasks

        fireNativeNotification(viewDestroyedEventName, frameName);
    });

    await loadFramework();

    // bind event listener object ahead-of-time
    await CefSharp.BindObjectAsync({ IgnoreCache: false }, eventListenerObjectName);

    bootstrapTask.setResult();
}

function createPropertiesProxy(basePropertiesObj: {}, nativeObjName: string, frameName: string): {} {
    const proxy = Object.assign({}, basePropertiesObj);
    Object.keys(proxy).forEach(key => {
        const value = basePropertiesObj[key];
        if (value !== undefined) {
            proxy[key] = value;
        } else {
            proxy[key] = async function () {
                let nativeObject = window[nativeObjName];
                if (!nativeObject) {
                    nativeObject = await new Promise(async (resolve) => {
                        await waitForNextPaint();
                        const nativeObject = await bindNativeObject(nativeObjName, frameName);
                        resolve(nativeObject);
                    });
                }
                return nativeObject[key].apply(window, arguments);
            };
        }
    });
    return proxy;
}

async function bindNativeObject(nativeObjectName: string, frameName: string) {
    await CefSharp.BindObjectAsync({ IgnoreCache: false }, nativeObjectName);

    // add to the native objects collection
    let frameNativeObjects = nativeObjectNames[frameName];
    if (!frameNativeObjects) {
        frameNativeObjects = [];
        nativeObjectNames[frameName] = frameNativeObjects;
    }
    frameNativeObjects.push(nativeObjectName);

    return window[nativeObjectName];
}

function handleError(error: Error) {
    if (enableDebugMode) {
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

function waitForDOMReady() {
    if (document.readyState === "loading") {
        return new Promise((resolve) => document.addEventListener("DOMContentLoaded", resolve, { once: true }));
    }
    return Promise.resolve();
}

function fireNativeNotification(eventName: string, ...args: string[]) {
    window[eventListenerObjectName].notify(eventName, ...args);
}

bootstrap();
