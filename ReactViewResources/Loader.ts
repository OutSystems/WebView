declare const CefSharp: {
    BindObjectAsync(objName1: string, objName2: string): Promise<void>
};

type Dictionary<T> = { [key: string]: T };

// TODO missing typings
type React = any;
type ReactDOM = any;

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

const [UseEnhancedRenderingEngine, EnableDebugMode, LibsPath, ModulesObjectName, EventListenerObjectName, ReadyEventName] = Array.from(new URLSearchParams(location.search).keys());

const Modules: Dictionary<{}> = {};
window[ModulesObjectName] = Modules;

const StylesheetsLoadTask = new Task();
const PluginsLoadTask = new Task();
const BootstrapTask = new Task();

export function showErrorMessage(msg: string): void {
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
        document.body.appendChild(msgContainer);
    }
    msgContainer.innerText = msg;
}

function loadRequire(): Promise<void> {
    return new Promise((resolve) => {
        // load require js
        let requireScript = document.createElement("script");
        requireScript.src = LibsPath + "requirejs/require.js";
        requireScript.addEventListener("load", () => resolve());
        if (document.head) {
            document.head.appendChild(requireScript);
        }
    });
}

function loadFramework(): void {
    if (UseEnhancedRenderingEngine === "1") {
        // load preact, instead
        define("preact", [LibsPath + "preact/dist/preact.min.js"], () => {
            // export preact as a module
            return window["preact"];
        });

        define("react-dom", ["react"], (react: React) => {
            // export react-dom as a module
            return react;
        });
    }

    const RequireCssPath = LibsPath + "require-css/css.min.js";

    require.config({
        paths: getRequirePaths(),
        map: {
            "*": {
                "css": RequireCssPath
            }
        }
    });

    require(["react", "react-dom", RequireCssPath]); // preload react and require-css
}

export function loadStyleSheet(stylesheet: string): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;

            await new Promise((resolve) => require(["css!" + stylesheet], resolve));

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

export function loadPlugins(plugins: string[][], mappings: Dictionary<string>): void {
    async function innerLoad() {
        try {
            await BootstrapTask.promise;

            if (mappings) {
                let paths = Object.assign(getRequirePaths(), mappings);
                require.config({
                    paths: paths
                });
            }

            if (plugins && plugins.length > 0) {
                // load plugin modules
                let pluginsPromises: Promise<void>[] = [];
                plugins.forEach(m => {
                    const ModuleName = m[0];
                    const NativeObjectName = m[1];
                    pluginsPromises.push(new Promise<void>((resolve, reject) => {
                        CefSharp.BindObjectAsync(NativeObjectName, NativeObjectName).then(() => {
                            require([ModuleName], (Module: any) => {
                                if (Module) {
                                    Modules[ModuleName] = Module.default;
                                    resolve();
                                } else {
                                    reject(`Failed to load '${ModuleName}' (might not be a module)`);
                                }
                            });
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

export function loadComponent(baseUrl: string, cacheInvalidationSuffix: string, hasStyleSheet: boolean, hasPlugins: boolean, userComponent: [string, string, string], userComponentNativeMethods: Dictionary<any>, mappings: Dictionary<string>): void {
    async function innerLoad() {
        try {
            // force images and other resources load from the appropriate path
            (document.getElementById("webview_base") as HTMLBaseElement).href = baseUrl;

            const [UserComponentNativeObjectName, UserComponentName, UserComponentSource] = userComponent;

            const RootElement = document.getElementById("webview_root") as HTMLDivElement;

            const CachedElementHtml = localStorage.getItem(UserComponentSource);
            if (CachedElementHtml) {
                // render cached component html to reduce time to first render
                RootElement.innerHTML = CachedElementHtml;
                await waitForNextPaint();
            }

            let promisesToWaitFor = [BootstrapTask.promise];
            if (hasStyleSheet) {
                promisesToWaitFor.push(StylesheetsLoadTask.promise);
            }
            if (hasPlugins) {
                promisesToWaitFor.push(PluginsLoadTask.promise);
            }
            await Promise.all(promisesToWaitFor);

            let paths = getRequirePaths();
            if (mappings) {
                paths = Object.assign(paths, mappings);
            }

            require.config({
                paths: paths,
                baseUrl: baseUrl,
                urlArgs: cacheInvalidationSuffix
            });

            // load component module
            const [React, ReactDOM, Component] = await new Promise<[React, ReactDOM, any]>((resolve, reject) => {
                require(["react", "react-dom", UserComponentSource], (React: React, ReactDOM: ReactDOM, UserComponentModule: any) => {
                    if (UserComponentModule.default) {
                        resolve([React, ReactDOM, UserComponentModule.default]);
                    } else {
                        reject(`Component module ('${UserComponentSource}') does not have a default export.`);
                    }
                });
            });

            // create proxy for properties obj to delay its methods execution until native object is ready
            const Properties = createPropertiesProxy(userComponentNativeMethods, UserComponentNativeObjectName);

            // render component
            await new Promise((resolve) => {
                Modules[UserComponentName] = ReactDOM.render(
                    React.createElement(Component, Properties),
                    RootElement,
                    resolve
                );
            });

            await waitForNextPaint();

            if (!CachedElementHtml) {
                // cache view html for further use
                const ElementHtml = RootElement.innerHTML;
                // get all stylesheets except the stick ones (which will be loaded by the time the html gets rendered) otherwise we could be loading them twice
                const Stylesheets = getAllStylesheets().filter(l => l.dataset.sticky !== "true").map(l => l.outerHTML).join("");
                localStorage.setItem(UserComponentSource, Stylesheets + ElementHtml);
            }

            window.dispatchEvent(new Event('viewready'));
            window[EventListenerObjectName].notify(ReadyEventName);
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

    await loadRequire();
    await loadFramework();

    // bind event listener object ahead-of-time
    await CefSharp.BindObjectAsync(EventListenerObjectName, EventListenerObjectName);

    BootstrapTask.setResult();
}

function createPropertiesProxy(basePropertiesObj: {}, nativeObjName: string): {} {
    let proxy = Object.assign({}, basePropertiesObj);
    Object.keys(proxy).forEach(key => {
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
    return document.head ? Array.from(document.head.querySelectorAll("link")) : [];
}

function getRequirePaths() {
    if (UseEnhancedRenderingEngine === "1") {
        return {
            "prop-types": LibsPath + "prop-types/prop-types.min",
            "react": LibsPath + "preact-compat/dist/preact-compat.min",
        };
    } else {
        // load react
        return {
            "prop-types": LibsPath + "prop-types/prop-types.min",
            "react": LibsPath + "react/umd/react.production.min",
            "react-dom": LibsPath + "react-dom/umd/react-dom.production.min",
        };
    }
}

bootstrap();
