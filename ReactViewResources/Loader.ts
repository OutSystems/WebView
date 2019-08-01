import * as Common from "./LoaderCommon";
import { Task } from "./LoaderCommon";
import * as PluginsProvider from "./PluginsProvider";
import { PluginsContext } from "./PluginsProvider";
import "./ViewFrame";

declare const CefSharp: {
    BindObjectAsync(objName1: string, objName2: string): Promise<void>
};

let rootContext: React.Context<string>;

const ReactLib: string = "React";
const ReactDOMLib: string = "ReactDOM";
const Bundles: string = "Bundle";

const [LibsPath, EnableDebugMode, ModulesObjectName, EventListenerObjectName, ViewInitializedEventName, ComponentLoadedEventName] = Array.from(new URLSearchParams(location.search).keys());

const ExternalLibsPath = LibsPath + "node_modules/";

const Modules: Dictionary<{}> = {};
window[ModulesObjectName] = Modules;

const BootstrapTask = new Task();
const StylesheetsLoadTask = new Task();
const PluginsLoadTasks: Dictionary<Task<any>> = {};

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
    await loadScript(ExternalLibsPath + "prop-types/prop-types.min.js"); /* Prop-Types */
    await loadScript(ExternalLibsPath + "react/umd/react.production.min.js"); /* React */ 
    await loadScript(ExternalLibsPath + "react-dom/umd/react-dom.production.min.js"); /* ReactDOM */
}

export function loadDefaultStyleSheet(stylesheet: string): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;
            await loadStyleSheet(stylesheet, document.head, true);

            StylesheetsLoadTask.setResult();
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

export function loadPlugins(plugins: any[][], frameName: string): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;

            const LoadTask = new Task();
            PluginsLoadTasks[frameName] = LoadTask;

            if (plugins && plugins.length > 0) {
                // load plugin modules
                let pluginsPromises: Promise<void>[] = plugins.map(async m => {
                    const ModuleName: string = m[0];
                    const ModuleInstanceName: string = m[1];
                    const MainJsSource: string = m[2];
                    const NativeObjectFullName: string = m[3]; // fullname with frame name included
                    const DependencySources: string[] = m[4];

                    if (frameName === "") {
                        // only load plugins sources once (in the main frame)

                        // load plugin dependency js sources
                        let dependencySourcesPromises: Promise<void>[] = [];
                        DependencySources.forEach(s => dependencySourcesPromises.push(loadScript(s)));
                        await Promise.all(dependencySourcesPromises);

                        // plugin main js source
                        await loadScript(MainJsSource);    
                    }

                    const Module = window[Bundles][ModuleName];
                    if (!Module || !Module.default) {
                        throw new Error(`Failed to load '${ModuleName}' (might not be a module with a default export)`);
                    }

                    await CefSharp.BindObjectAsync(NativeObjectFullName, NativeObjectFullName);

                    PluginsProvider.registerPlugin(frameName, Module.default, ModuleInstanceName, window[NativeObjectFullName]);
                });

                await Promise.all(pluginsPromises);
            }

            LoadTask.setResult();
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

            const RootElementData = Common.getViewElement(frameName);
            const RootElement = RootElementData.root;

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
                promisesToWaitFor.push(PluginsLoadTasks[frameName].promise);
            }
            await Promise.all(promisesToWaitFor);

            // load component dependencies js sources and css sources
            let loadDependencyPromises: Promise<void>[] = [];
            dependencySources.forEach(s => loadDependencyPromises.push(loadScript(s)));
            cssSources.forEach(s => loadDependencyPromises.push(loadStyleSheet(s, RootElementData.stylesheetsContainer, false)));
            await Promise.all(loadDependencyPromises);

            // main component script should be the last to be loaded, otherwise errors might occur
            await loadScript(componentSource);

            const Component = window[Bundles][componentName].default;
            const React = window[ReactLib];
            const ReactDOM = window[ReactDOMLib];
            
            // create proxy for properties obj to delay its methods execution until native object is ready
            const Properties = createPropertiesProxy(componentNativeObject, componentNativeObjectName);

            // render component
            await new Promise((resolve) => {
                if (!rootContext) {
                    rootContext = React.createContext(null);
                }
                Component.contextType = rootContext;
                const RootRef = React.createRef();
                const Root = React.createElement(rootContext.Provider, { value: new PluginsContext(frameName, Modules) }, React.createElement(Component, { ref: RootRef, ...Properties }));
                ReactDOM.hydrate(Root, RootElement, () => {
                    Modules[componentInstanceName] = RootRef.current;
                    resolve();
                });
            });

            await waitForNextPaint();

            if (!CachedElementHtml && maxPreRenderedCacheEntries > 0) {
                // cache view html for further use
                const ElementHtml = RootElement.innerHTML;
                // get all stylesheets except the stick ones (which will be loaded by the time the html gets rendered) otherwise we could be loading them twice
                const Stylesheets = Common.getStylesheets(RootElementData.stylesheetsContainer).filter(l => l.dataset.sticky !== "true").map(l => l.outerHTML).join("");

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

            fireNativeNotification(ComponentLoadedEventName, frameName);
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
    Common.addViewElement("", document.getElementById(Common.WebViewRootId) as HTMLElement, document.head);
    Common.addViewAddedEventListener((viewName) => fireNativeNotification(ViewInitializedEventName, viewName));
    Common.addViewRemovedEventListener((viewName) => {
        delete PluginsLoadTasks[viewName];
        PluginsProvider.unRegisterPlugins(viewName);
    });

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

function waitForDOMReady() {
    if (document.readyState === "loading") {
        return new Promise((resolve) => document.addEventListener("DOMContentLoaded", resolve, { once: true }));
    }
    return Promise.resolve();
}

function fireNativeNotification(eventName: string, ...args: string[]) {
    window[EventListenerObjectName].notify(eventName, ...args);
}

bootstrap();
