import * as React from 'react';
import * as ReactDOM from 'react-dom';

declare var NativeApi: any;
declare var __WebviewListener__: {
    notify: (eventName: string) => void;
};

declare var require: any;

export function initialize(componentUrl: string) {
    require([componentUrl], function (WebViewComponentModule: any) {
        let WebViewComponent = WebViewComponentModule.default;
        ReactDOM.render(
            <WebViewComponent />,
            document.getElementById("webview_root"),
            () => notifyNative("ready")
        );
    });
}

// wrap native api to send only track codes to .net
function wrapNativeApi(api: Object) {
    for (var member in api) {
        if (api.hasOwnProperty(member) && api[member] instanceof Function) {
            api[member] = new Proxy(api[member], {
                apply: function(target, thisArg, argumentsList) {
                    if (argumentsList.length > 0) {
                        var trackCode = argumentsList[0]["TrackCode"];
                        if (trackCode !== undefined) {
                            argumentsList[0] = trackCode;
                        }
                    }
                    
                    return target.apply(thisArg, argumentsList);
                }
            });
        }
    }
}

function notifyNative(eventName: string) {
    __WebviewListener__.notify(eventName)
}

if (typeof NativeApi !== undefined) {
    wrapNativeApi(NativeApi);
}
