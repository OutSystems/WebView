// Bridge between the page and .NET for the system web engines (WKWebView on macOS, WebView2 on Windows).
// It's injected at document start in the main frame and plays the role that CefGlue's javascript bindings play with CEF:
// - objects registered in .NET are exposed as global objects whose methods return promises
// - webviewBridge.checkObjectBound(name) resolves once the object is registered (true), waiting for the registration when
//   needed, or when the object is unregistered meanwhile (false)
// - .NET evaluates scripts through it and gets the JSON serialized result back
(function () {
    "use strict";

    if (window.webviewBridge || window.top !== window.self) {
        // already installed, or running in an iframe (only the main frame talks to .NET)
        return;
    }

    var webkitMessageHandler = window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.webviewBridge;
    var chromeWebView = window.chrome && window.chrome.webview;

    function post(message) {
        var json = JSON.stringify(message);
        if (webkitMessageHandler) {
            webkitMessageHandler.postMessage(json);
        } else if (chromeWebView) {
            chromeWebView.postMessage(json);
        }
    }

    var nextCallId = 1;
    var pendingCalls = new Map();
    var boundObjects = new Map();
    var bindingWaiters = new Map();

    function serializeArguments(args) {
        return JSON.stringify(Array.prototype.map.call(args, function (arg) {
            return arg === undefined ? null : arg;
        }));
    }

    function createMethod(objectName, methodName) {
        return function () {
            var args = serializeArguments(arguments);
            return new Promise(function (resolve, reject) {
                var id = nextCallId++;
                pendingCalls.set(id, { resolve: resolve, reject: reject });
                post({ type: "call", id: id, objectName: objectName, methodName: methodName, args: args });
            });
        };
    }

    function resolveBindingWaiters(name, isBound) {
        var waiters = bindingWaiters.get(name);
        if (waiters) {
            bindingWaiters.delete(name);
            waiters.forEach(function (resolve) {
                resolve(isBound);
            });
        }
    }

    function defineObject(name, methodNames) {
        var target = {};
        methodNames.forEach(function (methodName) {
            target[methodName] = createMethod(name, methodName);
        });
        boundObjects.set(name, target);
        window[name] = target;
        resolveBindingWaiters(name, true);
    }

    function deleteObject(name) {
        if (boundObjects.has(name) && window[name] === boundObjects.get(name)) {
            delete window[name];
        }
        boundObjects.delete(name);
        // whoever is waiting for the object won't get it
        resolveBindingWaiters(name, false);
    }

    function checkObjectBound(name) {
        if (boundObjects.has(name)) {
            return Promise.resolve(true);
        }
        return new Promise(function (resolve) {
            var waiters = bindingWaiters.get(name);
            if (!waiters) {
                waiters = [];
                bindingWaiters.set(name, waiters);
            }
            waiters.push(resolve);
            post({ type: "bindObject", name: name });
        });
    }

    function evaluate(id, script) {
        var message;
        try {
            var result = new Function(script)();
            var json = result === undefined ? undefined : JSON.stringify(result);
            message = { type: "evaluationResult", id: id, success: true, result: json === undefined ? null : json };
        } catch (error) {
            message = { type: "evaluationResult", id: id, success: false, error: String(error) };
        }
        post(message);
    }

    function completeCall(message) {
        var call = pendingCalls.get(message.id);
        if (!call) {
            return;
        }
        pendingCalls.delete(message.id);
        if (message.success) {
            call.resolve(message.result === null || message.result === undefined ? undefined : JSON.parse(message.result));
        } else {
            call.reject(message.error);
        }
    }

    function receive(message) {
        switch (message.type) {
            case "defineObjects":
                message.objects.forEach(function (object) {
                    defineObject(object.name, object.methods);
                });
                break;
            case "deleteObject":
                deleteObject(message.name);
                break;
            case "callResult":
                completeCall(message);
                break;
            case "evaluate":
                evaluate(message.id, message.script);
                break;
        }
    }

    if (chromeWebView) {
        chromeWebView.addEventListener("message", function (event) {
            receive(event.data);
        });
    }

    window.addEventListener("error", function (event) {
        var error = event.error;
        var isError = error instanceof Error;
        post({
            type: "uncaughtException",
            message: isError ? error.name + ": " + error.message : String(event.message),
            stack: isError && error.stack ? String(error.stack) : ""
        });
    });

    Object.defineProperty(window, "webviewBridge", {
        value: Object.freeze({
            checkObjectBound: checkObjectBound,
            deleteObjectBound: deleteObject,
            __receive: receive
        })
    });

    post({ type: "ready", url: location.href });
})();
