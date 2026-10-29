// Native <-> javascript bridge for the native web views (WKWebView / WebView2).
//
// Injected in the main frame of every document before any page script runs. It provides the same contract
// the (cef based) WebView used to provide to the pages:
//   - cefglue.checkObjectBound(name): binds a registered .NET object, available afterwards as window[name]
//   - window[name].method(...args): calls the .NET method, returns a promise with the result
// and runs scripts sent by the native side, reporting results/errors back.
// WebKit cancels a drag right after it started (dragend is raised a few ms after dragstart) when dataTransfer.setDragImage receives an image
// that has no content, e.g. "new Image()" that is used to hide the drag image. Chromium accepts it, so pages written for it (the drag and drop
// library of the design system does it on every drag) never start dragging. An empty image is replaced by a preloaded transparent pixel.
(function () {
    var dataTransfer = window.DataTransfer;
    if (!dataTransfer || !dataTransfer.prototype || !dataTransfer.prototype.setDragImage || typeof Image === "undefined") {
        return;
    }

    var originalSetDragImage = dataTransfer.prototype.setDragImage;
    var transparentPixel = new Image(); // preloaded, it has to be loaded when the drag starts
    transparentPixel.src = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

    dataTransfer.prototype.setDragImage = function (image, x, y) {
        var isEmptyImage = image instanceof HTMLImageElement && image.naturalWidth === 0;
        return originalSetDragImage.call(this, isEmptyImage ? transparentPixel : image, x, y);
    };
})();

// TEMPORARY drag and drop tracing (needs WEBVIEW_NATIVE_TRACE=1); remove after the investigation.
// Runs in every frame (the bridge below only runs in the top frame): view frames of ReactView are iframes.
(function () {
    function log(text) {
        // visible in the web inspector console of the frame, and in the native trace (stderr) when WEBVIEW_NATIVE_TRACE=1
        console.log(text);
        try {
            var handler = window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.rvBridge;
            if (handler) {
                handler.postMessage(JSON.stringify({ t: "console", level: "log", text: text }));
            }
        } catch (e) {
            // ignore
        }
    }
    var where = function () {
        var isTop = false;
        try { isTop = window.top === window; } catch (e) { /* cross origin */ }
        return (isTop ? "top" : "iframe") + " " + location.href.substring(0, 80);
    };
    // drag/dragover fire continuously: only log the first few of each drag (the counters restart on every dragstart)
    var counts = { drag: 0, dragover: 0 };
    ["mousedown", "dragstart", "drag", "dragenter", "dragover", "dragleave", "drop", "dragend", "mouseup"].forEach(function (name) {
        document.addEventListener(name, function (e) {
            if (name === "dragstart") {
                counts.drag = 0;
                counts.dragover = 0;
            }
            if ((name === "drag" || name === "dragover") && ++counts[name] > 3) {
                return;
            }
            var dt = e.dataTransfer;
            // the view frames render their views inside open shadow roots: the event target is retargeted to the host,
            // the composed path has the real element and its ancestors (across the shadow boundaries)
            var path = e.composedPath ? e.composedPath() : [];
            var describe = function (el) { return el && el.tagName ? el.tagName + "." + (el.getAttribute && el.getAttribute("class") || "") : String(el); };
            var draggables = path.filter(function (el) { return el.draggable === true; }).map(describe);
            log("[DND] " + name + " t=" + Math.round(performance.now()) +
                " target=" + describe(e.target) +
                " realTarget=" + describe(path[0]) +
                " draggablesInPath=" + JSON.stringify(draggables) +
                " defaultPrevented=" + e.defaultPrevented +
                " types=" + (dt ? JSON.stringify(Array.prototype.slice.call(dt.types)) + " effectAllowed=" + dt.effectAllowed + " dropEffect=" + dt.dropEffect : "n/a") +
                " xy=" + e.clientX + "," + e.clientY + " frame=[" + where() + "]");
            if (name === "drop" && dt) {
                // the data is only readable during the drop; an empty value for a listed type means it is not shared with this document
                Array.prototype.forEach.call(dt.types, function (type) {
                    var value;
                    try { value = dt.getData(type); } catch (err) { value = "<error " + err + ">"; }
                    log("[DND] drop data type=" + type + " length=" + (value ? value.length : 0) + " value=" + (value || "").substring(0, 60));
                });
            }
        }, true);
    });
    // after the page handlers ran (bubble phase on window): was the event accepted as a drop target? (a drop only happens when dragenter/dragover were cancelled
    // and the dropEffect is allowed by the effectAllowed of the source)
    // keyboard: does the page receive the keys (shortcuts are raised from keydown handlers of the views), and where is the focus?
    ["keydown", "keyup", "copy", "cut", "paste"].forEach(function (name) {
        document.addEventListener(name, function (e) {
            var active = document.activeElement;
            while (active && active.shadowRoot && active.shadowRoot.activeElement) {
                active = active.shadowRoot.activeElement;
            }
            var path = e.composedPath ? e.composedPath() : [];
            log("[KEY] " + name + " key=" + e.key + " keyCode=" + e.keyCode + " ctrl=" + e.ctrlKey + " alt=" + e.altKey + " shift=" + e.shiftKey + " meta=" + e.metaKey +
                " realTarget=" + (path[0] && path[0].tagName ? path[0].tagName + "." + (path[0].getAttribute("class") || "") : "?") +
                " active=" + (active && active.tagName ? active.tagName + "." + (active.getAttribute("class") || "") : "none") +
                " hasFocus=" + document.hasFocus() + " defaultPrevented(before handlers)=" + e.defaultPrevented);
        }, true);
        window.addEventListener(name, function (e) {
            if (name === "keydown") {
                log("[KEY] after-handlers keydown key=" + e.key + " defaultPrevented=" + e.defaultPrevented);
            }
        }, false);
    });

    // the drop effect is updated from the answer of the .net side, so a dragover is logged when its outcome changes
    var lastOutcome = "";
    ["dragenter", "dragover", "drop", "dragleave"].forEach(function (name) {
        window.addEventListener(name, function (e) {
            var outcome = e.defaultPrevented + "/" + (e.dataTransfer && e.dataTransfer.dropEffect) + "/" + (e.dataTransfer && e.dataTransfer.effectAllowed);
            if (name === "dragover" && outcome === lastOutcome) {
                return;
            }
            lastOutcome = outcome;
            var path = e.composedPath ? e.composedPath() : [];
            log("[DND] after-handlers " + name + " defaultPrevented=" + e.defaultPrevented + " dropEffect=" + (e.dataTransfer && e.dataTransfer.dropEffect) +
                " effectAllowed=" + (e.dataTransfer && e.dataTransfer.effectAllowed) + " realTarget=" + (path[0] && path[0].tagName ? path[0].tagName + "." + (path[0].getAttribute("class") || "") : "?"));
        }, false);
    });
    // nothing at all after the last dragover/dragenter: log when a drag leaves without a drop
    log("[DND] document ready frame=[" + where() + "] origin=" + window.origin);
})();

(function () {
    "use strict";

    // only the top frame is bridged (WebView2 runs the script in every frame)
    try {
        if (window.top !== window) {
            return;
        }
    } catch (e) {
        return;
    }

    if (window.__rv) {
        return;
    }

    var pendingCalls = new Map();
    var pendingBinds = new Map();
    var nextCallId = 1;
    var traceEnabled = false;
    var keyboardDisabled = false;
    var contextMenuDisabled = false;
    var developerKeysEnabled = false;

    function postRaw(text) {
        var handler = window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.rvBridge;
        if (handler) {
            handler.postMessage(text); // WKWebView
        } else if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(text); // WebView2
        }
    }

    function post(message) {
        postRaw(JSON.stringify(message));
    }

    function toBase64(bytes) {
        var chunk = 0x8000;
        var text = "";
        for (var i = 0; i < bytes.length; i += chunk) {
            text += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        }
        return btoa(text);
    }

    // typed arrays are sent as base64 strings (binary .net parameters/results are base64 encoded too)
    function replacer(key, value) {
        if (value instanceof Uint8Array) {
            return toBase64(value);
        }
        if (value === undefined) {
            return null;
        }
        return value;
    }

    function serialize(value) {
        return value === undefined ? "null" : JSON.stringify(value, replacer);
    }

    function describeError(error) {
        if (error && typeof error === "object") {
            return {
                name: String(error.name || "Error"),
                message: String(error.message === undefined ? error : error.message),
                stack: String(error.stack || "")
            };
        }
        return { name: "Error", message: String(error), stack: "" };
    }

    function callNative(objectName, methodName, args) {
        return new Promise(function (resolve, reject) {
            var id = nextCallId++;
            var argsJson;
            try {
                argsJson = serialize(Array.prototype.slice.call(args));
            } catch (e) {
                reject(e);
                return;
            }
            pendingCalls.set(id, { resolve: resolve, reject: reject });
            post({ t: "call", id: id, obj: objectName, method: methodName, args: argsJson });
        });
    }

    var bridge = {

        // ---- native -> js ----------------------------------------------------------------------------------------

        // defines window[name], exposing the specified methods as functions that call the .net object
        defineObject: function (name, methods) {
            if (Object.prototype.hasOwnProperty.call(window, name)) {
                return;
            }
            var proxy = {};
            methods.forEach(function (methodName) {
                proxy[methodName] = function () {
                    return callNative(name, methodName, arguments);
                };
            });
            Object.defineProperty(window, name, { value: Object.freeze(proxy), configurable: true, enumerable: false, writable: false });
        },

        undefineObject: function (name) {
            delete window[name];
        },

        bound: function (name, success, error) {
            var waiters = pendingBinds.get(name);
            pendingBinds.delete(name);
            if (waiters) {
                waiters.forEach(function (waiter) {
                    if (success) {
                        waiter.resolve(true);
                    } else {
                        waiter.reject(new Error(error || ("Object named " + name + " was not found. Make sure it was registered before.")));
                    }
                });
            }
        },

        resolve: function (id, value) {
            var call = pendingCalls.get(id);
            if (call) {
                pendingCalls.delete(id);
                call.resolve(value);
            }
        },

        reject: function (id, message) {
            var call = pendingCalls.get(id);
            if (call) {
                pendingCalls.delete(id);
                call.reject(new Error(message));
            }
        },

        // ---- results of the scripts executed by the native side -------------------------------------------------

        // id 0 is used for scripts whose result is not awaited (errors are reported as unhandled exceptions)
        reply: function (id, value) {
            var json;
            try {
                json = serialize(value);
            } catch (e) {
                bridge.fail(id, e);
                return;
            }
            post({ t: "res", id: id, ok: true, v: json });
        },

        // fn: name of the function being executed (the arguments are not reported, they can contain sensitive information)
        fail: function (id, error, fn) {
            post({ t: "res", id: id, ok: false, v: describeError(error), fn: fn || "" });
        },

        // ---- configuration ---------------------------------------------------------------------------------------

        setKeyboardDisabled: function (disabled) {
            keyboardDisabled = !!disabled;
        },

        setContextMenuDisabled: function (disabled) {
            contextMenuDisabled = !!disabled;
        },

        setTrace: function (enabled) {
            traceEnabled = !!enabled;
        },

        // the native view receives the key events directly, so F12 (toggle developer tools) is detected here
        setDeveloperKeysEnabled: function (enabled) {
            developerKeysEnabled = !!enabled;
        }
    };

    // same contract used by the loader to wait for a native object to be available
    window.cefglue = {
        checkObjectBound: function (name) {
            if (Object.prototype.hasOwnProperty.call(window, name)) {
                return Promise.resolve(true);
            }
            return new Promise(function (resolve, reject) {
                var waiters = pendingBinds.get(name);
                if (!waiters) {
                    waiters = [];
                    pendingBinds.set(name, waiters);
                    post({ t: "bind", name: name });
                }
                waiters.push({ resolve: resolve, reject: reject });
            });
        },

        deleteObjectBound: function (name) {
            delete window[name];
        }
    };

    Object.defineProperty(window, "__rv", { value: Object.freeze(bridge), configurable: false, enumerable: false, writable: false });

    // ---- uncaught errors ---------------------------------------------------------------------------------------

    // Browsers dispatch this as an error event, but it is only a notice (the observer could not deliver every notification in the same frame,
    // it will in the next one) and it is not an exception. Chromium (cef) did not report it as one, and it repeats on every frame while it lasts.
    var benignErrorMessage = /^ResizeObserver loop (limit exceeded|completed with undelivered notifications)/;

    window.addEventListener("error", function (e) {
        // only runtime errors reach the window (resource load errors do not bubble)
        var error = e.error;
        if (!error && benignErrorMessage.test(e.message || "")) {
            return;
        }
        post({
            t: "jserr",
            message: (error && error.message) || e.message || "Uncaught error",
            name: (error && error.name) || "Error",
            stack: (error && error.stack) || "",
            url: e.filename || "",
            line: e.lineno || 0,
            col: e.colno || 0
        });
    });

    window.addEventListener("unhandledrejection", function (e) {
        var reason = e.reason;
        var info = describeError(reason);
        post({ t: "jserr", message: info.message, name: info.name, stack: info.stack, url: "", line: 0, col: 0 });
    });

    // ---- input ---------------------------------------------------------------------------------------------------

    ["keydown", "keypress", "keyup"].forEach(function (type) {
        window.addEventListener(type, function (e) {
            if (keyboardDisabled) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        }, true);
    });

    window.addEventListener("keydown", function (e) {
        if (developerKeysEnabled && e.key === "F12") {
            post({ t: "f12" });
        }
    }, true);

    window.addEventListener("contextmenu", function (e) {
        if (contextMenuDisabled) {
            e.preventDefault();
        }
    }, true);

    // ---- diagnostics (only forwarded to native when tracing is enabled) ------------------------------------------

    ["log", "warn", "error"].forEach(function (level) {
        var original = console[level];
        console[level] = function () {
            if (traceEnabled) {
                try {
                    post({ t: "console", level: level, text: Array.prototype.map.call(arguments, String).join(" ") });
                } catch (e) { /* ignore */ }
            }
            return original.apply(console, arguments);
        };
    });

    // failed requests are only reported in the console as "Load failed", without the url: trace them
    var originalFetch = window.fetch;
    if (originalFetch) {
        window.fetch = function (input) {
            var result = originalFetch.apply(this, arguments);
            result.catch(function (e) {
                if (traceEnabled) {
                    post({ t: "console", level: "error", text: "fetch failed: " + ((input && input.url) || input) + " (" + e + ")" });
                }
            });
            return result;
        };
    }
    var originalOpen = XMLHttpRequest.prototype.open;
    XMLHttpRequest.prototype.open = function (method, url) {
        this.addEventListener("error", function () {
            if (traceEnabled) {
                post({ t: "console", level: "error", text: "xhr failed: " + method + " " + url });
            }
        });
        return originalOpen.apply(this, arguments);
    };

    // the javascript context of the document is ready, native can start flushing scripts
    post({ t: "ctx" });
})();
