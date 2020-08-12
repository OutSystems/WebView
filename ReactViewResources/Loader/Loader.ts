﻿/// <reference path="./../../ViewGenerator/contentFiles/global.d.ts"/>

import { getStylesheets, webViewRootId, mainFrameName } from "./LoaderCommon";
import { ObservableListCollection } from "./ObservableCollection";
import { Task } from "./Task";
import { ViewMetadata } from "./ViewMetadata";

declare function define(name: string, dependencies: string[], definition: Function);

declare const cefglue: {
    checkObjectBound(objName: string): Promise<boolean>
};

interface PromiseWithFinally<T> extends Promise<T> {
    finally(onFinally: () => void);
}

export const syncFunction = new Object();

const reactLib: string = "React";
const reactDOMLib: string = "ReactDOM";
const viewsBundleName: string = "Views";
const pluginsBundleName: string = "Plugins";

let defaultStyleSheetLink: HTMLLinkElement;

const [
    libsPath,
    enableDebugMode,
    modulesFunctionName,
    eventListenerObjectName,
    viewInitializedEventName,
    viewDestroyedEventName,
    viewLoadedEventName,
    disableKeyboardCallback,
    customResourceBaseUrl
] = Array.from(new URLSearchParams(location.search).keys());

const defaultLoadResourcesTimeout = (enableDebugMode ? 60 : 10) * 1000; // ms

const externalLibsPath = libsPath + "node_modules/";

const bootstrapTask = new Task();
const defaultStylesheetLoadTask = new Task();
const views = new Map<string, ViewMetadata>();

class TimeoutException extends Error { }

function getView(viewName: string): ViewMetadata {
    const view = views.get(viewName);
    if (!view) {
        throw new Error(`View "${viewName}" not loaded`);
    }
    return view;
}

function getModule(viewName: string, id: string, moduleName: string) {
    const view = views.get(viewName);
    if (view && view.id.toString() === id) {
        // when generation requested != current generation, ignore (we don't want to interact with old views)
        const module = view.modules.get(moduleName);
        if (module) {
            return module;
        }
    }

    return new Proxy({}, {
        get: function () {
            // return a dummy function, call will be ingored, but no exception thrown
            return new Function();
        }
    });
}

window[modulesFunctionName] = getModule;

export async function showErrorMessage(msg: string): Promise<void> {
    return showMessage(msg, true);
}

function showWarningMessage(msg: string): Promise<void> {
    return showMessage(msg, false);
}

async function showMessage(msg: string, isError: boolean): Promise<void> {
    const containerId = "webview_error";
    let msgContainer = document.getElementById(containerId) as HTMLDivElement;
    if (!msgContainer) {
        msgContainer = document.createElement("div");
        msgContainer.id = containerId;
        const style = msgContainer.style;
        style.backgroundColor = isError ? "#f45642" : "#f4b942";
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
        style.visibility = "visible";

        await waitForDOMReady();
        document.body.appendChild(msgContainer);
    }
    msgContainer.innerText = msg;

    if (!isError) {
        setTimeout(() => {
            msgContainer.style.visibility = "collapsed";
        }, 30000);
    }
}

function loadScript(scriptSrc: string, view: ViewMetadata): Promise<void> {
    return new Promise(async (resolve, reject) => {
        const frameScripts = view.scriptsLoadTasks;

        // check if script was already added, fallback to main frame
        let scriptLoadTask = frameScripts.get(scriptSrc) || getView(mainFrameName).scriptsLoadTasks.get(scriptSrc);
        if (scriptLoadTask) {
            // wait for script to be loaded
            await scriptLoadTask.promise;
            resolve();
            return;
        }

        const loadTask = new Task<void>();
        frameScripts.set(scriptSrc, loadTask);

        const script = document.createElement("script");
        script.src = scriptSrc;

        waitForLoad(script, scriptSrc, defaultLoadResourcesTimeout)
            .then(() => {
                loadTask.setResult();
                resolve();
            });

        if (!view.head) {
            throw new Error(`View ${view.name} head is not set`);
        }
        view.head.appendChild(script);
    });
}

function loadStyleSheet(stylesheet: string, containerElement: Element, markAsSticky: boolean): Promise<void> {
    return new Promise((resolve, reject) => {
        const link = document.createElement("link");
        link.type = "text/css";
        link.rel = "stylesheet";
        link.href = stylesheet;
        if (markAsSticky) {
            link.dataset.sticky = "true";
        }

        waitForLoad(link, stylesheet, defaultLoadResourcesTimeout)
            .then(resolve);

        containerElement.appendChild(link);

        if (!defaultStyleSheetLink) {
            defaultStyleSheetLink = link;
        }
    });
}

export function setDefaultStyleSheet(stylesheet: string): void {
    if (defaultStyleSheetLink) {
        defaultStyleSheetLink.setAttribute("href", stylesheet);
    }
}

export function loadDefaultStyleSheet(stylesheet: string): void {
    async function innerLoad() {
        try {
            await bootstrapTask.promise;
            await loadStyleSheet(stylesheet, document.head, true);

            defaultStylesheetLoadTask.setResult();
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

            const view = getView(frameName);

            if (!view.isMain) {
                // wait for main frame plugins to be loaded, otherwise modules won't be loaded yet
                await getView(mainFrameName).pluginsLoadTask.promise;
            }

            if (plugins && plugins.length > 0) {
                // load plugin modules
                const pluginsPromises = plugins.map(async m => {
                    const moduleName: string = m[0];
                    const mainJsSource: string = m[1];
                    const nativeObjectFullName: string = m[2]; // fullname with frame name included
                    const dependencySources: string[] = m[3];

                    if (view.isMain) {
                        // only load plugins sources once (in the main frame)
                        // load plugin dependency js sources
                        const dependencySourcesPromises = dependencySources.map(s => loadScript(s, view));
                        await Promise.all(dependencySourcesPromises);

                        // plugin main js source
                        await loadScript(mainJsSource, view);
                    }

                    const pluginsBundle = window[pluginsBundleName];
                    const module = (pluginsBundle ? pluginsBundle[moduleName] : null) || window[viewsBundleName][moduleName];
                    if (!module || !module.default) {
                        throw new Error(`Failed to load '${moduleName}' (might not be a module with a default export)`);
                    }

                    const pluginNativeObject = await bindNativeObject(nativeObjectFullName);

                    view.nativeObjectNames.push(nativeObjectFullName); // add to the native objects collection
                    view.modules.set(moduleName, new module.default(pluginNativeObject, view.root));
                });

                await Promise.all(pluginsPromises);
            }

            view.pluginsLoadTask.setResult();
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
    componentNativeObject: any,
    frameName: string,
    componentHash: string): void {

    function getComponentCacheKey(propertiesHash: string) {
        return componentSource + "|" + propertiesHash;
    }

    async function innerLoad() {
        try {
            if (hasStyleSheet) {
                // wait for the stylesheet to load before first render
                await defaultStylesheetLoadTask.promise;
            }

            const view = getView(frameName);
            const head = view.head;
            const rootElement = view.root;

            if (!rootElement || !head) {
                throw new Error(`View ${view.name} head or root is not set`);
            }

            const componentCacheKey = getComponentCacheKey(componentHash);
            const enableHtmlCache = view.isMain; // disable cache retrieval for inner views, since react does not currently support portals hydration
            const cachedComponentHtml = enableHtmlCache ? localStorage.getItem(componentCacheKey) : null;
            const shouldStoreComponentHtml = enableHtmlCache && !cachedComponentHtml && maxPreRenderedCacheEntries > 0;
            if (cachedComponentHtml) {
                // render cached component html to reduce time to first render
                rootElement.innerHTML = cachedComponentHtml;
                await waitForNextPaint();
            }

            const promisesToWaitFor = [bootstrapTask.promise];
            if (hasPlugins) {
                promisesToWaitFor.push(view.pluginsLoadTask.promise);
            }
            await Promise.all(promisesToWaitFor);

            // load component dependencies js sources and css sources
            const dependencyLoadPromises =
                dependencySources.map(s => loadScript(s, view)).concat(
                    cssSources.map(s => loadStyleSheet(s, head, false)));
            await Promise.all(dependencyLoadPromises);

            // main component script should be the last to be loaded, otherwise errors might occur
            await loadScript(componentSource, view);

            const renderTask = shouldStoreComponentHtml ? new Task<void>() : undefined;

            // create proxy for properties obj to delay its methods execution until native object is ready
            const properties = createPropertiesProxy(componentNativeObject, componentNativeObjectName, renderTask);
            view.nativeObjectNames.push(componentNativeObjectName); // add to the native objects collection

            const componentClass = (window[viewsBundleName][componentName] || {}).default;
            if (!componentClass) {
                throw new Error(`Component ${componentName} is not defined or does not have a default class`);
            }

            const { createView } = await import("./Loader.View");

            const viewElement = createView(componentClass, properties, view, componentName, onChildViewAdded, onChildViewRemoved);
            const render = view.renderHandler;
            if (!render) {
                throw new Error(`View ${view.name} render handler is not set`);
            }

            await render(viewElement);

            await waitForNextPaint();

            if (shouldStoreComponentHtml) {
                // cache view html for further use
                const elementHtml = rootElement.innerHTML;
                // get all stylesheets except the stick ones (which will be loaded by the time the html gets rendered) otherwise we could be loading them twice
                const stylesheets = getStylesheets(head).filter(l => l.dataset.sticky !== "true").map(l => l.outerHTML).join("");

                // pending native calls can now be resolved, first html snapshot was grabbed
                renderTask!.setResult();

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

            fireNativeNotification(viewLoadedEventName, view.name, view.id.toString());
        } catch (error) {
            handleError(error);
        }
    }

    innerLoad();
}

async function bootstrap() {
    function preventDroppingFiles(event: DragEvent): void {
        const containsDraggedFiles = event.dataTransfer && event.dataTransfer.types.includes("Files");
        if (containsDraggedFiles) {
            event.preventDefault();
        }
    }

    window.addEventListener("dragover", preventDroppingFiles);
    window.addEventListener("drop", preventDroppingFiles);

    await waitForDOMReady();

    const rootElement = document.getElementById(webViewRootId);
    if (!rootElement) {
        throw new Error("Root element not found");
    }

    const mainView: ViewMetadata = {
        id: 0,
        name: mainFrameName,
        generation: 0,
        isMain: true,
        placeholder: rootElement,
        head: document.head,
        root: rootElement,
        modules: new Map<string, any>(),
        nativeObjectNames: [],
        pluginsLoadTask: new Task(),
        scriptsLoadTasks: new Map<string, Task<void>>(),
        childViews: new ObservableListCollection<ViewMetadata>(),
        parentView: null!
    };
    views.set(mainFrameName, mainView);

    await loadFramework();

    const { renderMainView } = await import("./Loader.View");
    mainView.renderHandler = component => renderMainView(component, rootElement);

    const resourceLoader = await import("./ResourceLoader");
    resourceLoader.setCustomResourceBaseUrl(customResourceBaseUrl);

    // bind event listener object ahead-of-time
    await cefglue.checkObjectBound(eventListenerObjectName);

    bootstrapTask.setResult();

    fireNativeNotification(viewInitializedEventName, mainFrameName);
}

async function loadFramework(): Promise<void> {
    const view = getView(mainFrameName);
    await loadScript(externalLibsPath + "prop-types/prop-types.min.js", view); /* Prop-Types */
    await loadScript(externalLibsPath + "react/umd/react.production.min.js", view); /* React */
    await loadScript(externalLibsPath + "react-dom/umd/react-dom.production.min.js", view); /* ReactDOM */

    define("react", [], () => window[reactLib]);
    define("react-dom", [], () => window[reactDOMLib]);
}

function createPropertiesProxy(objProperties: {}, nativeObjName: string, componentRenderedWaitTask?: Task<void>): {} {
    const proxy = Object.assign({}, objProperties);
    Object.keys(proxy).forEach(key => {
        const value = objProperties[key];
        const isSyncFunction = value === syncFunction;
        if (value !== undefined && !isSyncFunction) {
            proxy[key] = value;
        } else {
            proxy[key] = async function () {
                let nativeObject = window[nativeObjName];
                if (!nativeObject) {
                    nativeObject = await new Promise(async (resolve) => {
                        const nativeObject = await bindNativeObject(nativeObjName);
                        resolve(nativeObject);
                    });
                }

                let result: PromiseWithFinally<any> | undefined = undefined;
                try {
                    if (isSyncFunction) {
                        disableInputInteractions(true);
                    }

                    result = nativeObject[key].apply(window, arguments);
                } finally {
                    if (isSyncFunction) {
                        if (result) {
                            result.finally(() => disableInputInteractions(false));
                        } else {
                            disableInputInteractions(false);
                        }
                    }
                }

                if (componentRenderedWaitTask) {
                    // wait until component is rendered, first render should only render static data
                    await componentRenderedWaitTask.promise;
                }

                return result;
            };
        }
    });
    return proxy;
}

async function bindNativeObject(nativeObjectName: string) {
    await cefglue.checkObjectBound(nativeObjectName);
    return window[nativeObjectName];
}

function handleError(error: Error | string) {
    if (enableDebugMode) {
        const msg = error instanceof Error ? error.message : error;
        showErrorMessage(msg);
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


function waitForLoad(element: HTMLElement, url: string, timeout: number) {
    return new Promise((resolve) => {
        const timeoutHandle = setTimeout(
            () => {
                if (enableDebugMode) {
                    showWarningMessage(`Timeout loading resouce: '${url}'. If you paused the application to debug, you may disregard this message.`);
                }
            },
            timeout);

        element.addEventListener("load", () => {
            clearTimeout(timeoutHandle);
            resolve();
        });
    });
}


function fireNativeNotification(eventName: string, ...args: string[]) {
    window[eventListenerObjectName].notify(eventName, ...args);
}

function onChildViewAdded(childView: ViewMetadata) {
    views.set(childView.name, childView);
    fireNativeNotification(viewInitializedEventName, childView.name);
}

function onChildViewRemoved(childView: ViewMetadata) {
    views.delete(childView.name);
    fireNativeNotification(viewDestroyedEventName, childView.name, childView.id.toString());
}

export const disableInputInteractions = (() => {
    let layer: HTMLDivElement;
    let disableCounter = 0;
    return (disable: boolean) => {
        if (disable) {
            disableCounter++;
            if (disableCounter > 1) {
                return;
            }

            if (!layer) {
                layer = layer || document.createElement("div");
                layer.id = "webview_root_layer"; // used to enable styling by consumers
                layer.style.userSelect = "none";
                layer.style.height = "100vh";
                layer.style.width = "100vw";
                layer.style.position = "fixed";
                layer.style.top = "0";
                layer.style.left = "0";
                layer.style.zIndex = "2147483647";
            }

            alert(disableKeyboardCallback + "1");
            document.body.appendChild(layer);
        } else {
            if (disableCounter === 1) {
                // going 0
                alert(disableKeyboardCallback + "0");
                document.body.removeChild(layer);
            }
            disableCounter = Math.max(0, disableCounter - 1);
        }
    };
})();

bootstrap();