import * as Common from "./LoaderCommon";
import { Task } from "./LoaderCommon";
import * as PluginsProvider from "./PluginsProvider";
import { PluginsContext } from "./PluginsProvider";
import "./ViewFrame";

declare const CefSharp: {
    BindObjectAsync(objName1: string, objName2: string): Promise<void>
};

let rootContext: React.Context<string>;

const reactLib: string = "React";
const reactDOMLib: string = "ReactDOM";
const bundlesName: string = "Bundle";

const [
    libsPath,
    enableDebugMode,
    modulesObjectName,
    eventListenerObjectName,
    viewInitializedEventName,
    componentLoadedEventName
] = Array.from(new URLSearchParams(location.search).keys());

const externalLibsPath = libsPath + "node_modules/";

const modules: Dictionary<{}> = (() => window[modulesObjectName] = {})();

const bootstrapTask = new Task();
const stylesheetsLoadTask = new Task();
const pluginsLoadTasks: Dictionary<Task<any>> = {};

export async function showErrorMessage(msg: string): Promise<void> {
    const containerId = "webview_error";
    let msgContainer = document.getElementById(containerId) as HTMLDivElement;
    if (!msgContainer) {
        msgContainer = document.createElement("div");
        msgContainer.id = containerId;
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

function loadStyleSheet(stylesheet: string, containerElement: HTMLElement, markAsSticky: boolean): Promise<void> {
    return new Promise((resolve) => {
        let link = document.createElement("link");
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

export function loadPlugins(plugins: any[][], frameName: string): void {
    async function innerLoad() {
        try {
            await bootstrapTask.promise;

            const loadTask = new Task();
            pluginsLoadTasks[frameName] = loadTask;

            if (plugins && plugins.length > 0) {
                // load plugin modules
                let pluginsPromises: Promise<void>[] = plugins.map(async m => {
                    const moduleName: string = m[0];
                    const moduleInstanceName: string = m[1];
                    const mainJsSource: string = m[2];
                    const nativeObjectFullName: string = m[3]; // fullname with frame name included
                    const dependencySources: string[] = m[4];

                    if (frameName === "") {
                        // only load plugins sources once (in the main frame)

                        // load plugin dependency js sources
                        let dependencySourcesPromises: Promise<void>[] = [];
                        dependencySources.forEach(s => dependencySourcesPromises.push(loadScript(s)));
                        await Promise.all(dependencySourcesPromises);

                        // plugin main js source
                        await loadScript(mainJsSource);    
                    }

                    const module = window[bundlesName][moduleName];
                    if (!module || !module.default) {
                        throw new Error(`Failed to load '${moduleName}' (might not be a module with a default export)`);
                    }

                    await CefSharp.BindObjectAsync(nativeObjectFullName, nativeObjectFullName);

                    PluginsProvider.registerPlugin(frameName, module.default, moduleInstanceName, window[nativeObjectFullName]);
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
    componentInstanceName: string,
    componentNativeObjectName: string,
    componentSource: string,
    originalSourceFolder: string,
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
            if (frameName === "") {
                // loading main view
                // force images and other resources load from the appropriate path
                (document.getElementById("webview_base") as HTMLBaseElement).href = originalSourceFolder;
            }

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

            let promisesToWaitFor = [bootstrapTask.promise];
            if (hasPlugins) {
                promisesToWaitFor.push(pluginsLoadTasks[frameName].promise);
            }
            await Promise.all(promisesToWaitFor);

            // load component dependencies js sources and css sources
            let loadDependencyPromises: Promise<void>[] = [];
            dependencySources.forEach(s => loadDependencyPromises.push(loadScript(s)));
            cssSources.forEach(s => loadDependencyPromises.push(loadStyleSheet(s, rootElementData.stylesheetsContainer, false)));
            await Promise.all(loadDependencyPromises);

            // main component script should be the last to be loaded, otherwise errors might occur
            await loadScript(componentSource);

            const Component = window[bundlesName][componentName].default;
            const React = window[reactLib];
            const ReactDOM = window[reactDOMLib];
            
            // create proxy for properties obj to delay its methods execution until native object is ready
            const properties = createPropertiesProxy(componentNativeObject, componentNativeObjectName);

            // render component
            await new Promise((resolve) => {
                // create context
                if (!rootContext) {
                    rootContext = React.createContext(null);
                }
                Component.contextType = rootContext;

                const rootRef = React.createRef();
                const root = React.createElement(rootContext.Provider, { value: new PluginsContext(frameName, modules) }, React.createElement(Component, { ref: rootRef, ...properties }));

                ReactDOM.hydrate(root, rootElement, () => {
                    modules[componentInstanceName] = rootRef.current;
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

            fireNativeNotification(componentLoadedEventName, frameName);
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
    Common.addViewElement("", document.getElementById(Common.webViewRootId) as HTMLElement, document.head);
    Common.addViewAddedEventListener((viewName) => fireNativeNotification(viewInitializedEventName, viewName));
    Common.addViewRemovedEventListener((viewName) => {
        delete pluginsLoadTasks[viewName];
        PluginsProvider.unRegisterPlugins(viewName);
    });

    await loadFramework();

    // bind event listener object ahead-of-time
    await CefSharp.BindObjectAsync(eventListenerObjectName, eventListenerObjectName);

    bootstrapTask.setResult();
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
