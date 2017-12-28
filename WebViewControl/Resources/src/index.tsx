import * as React from "react";
import * as ReactDOM from "react-dom";
import "object-tracker";

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
            () => notifyNative("Ready")
        );
    });
}

function notifyNative(eventName: string) {
    __WebviewListener__.notify(eventName);
}
