import * as React from "react";
import * as ReactDOM from "react-dom";

declare var __WebviewListener__: {
    notify: (eventName: string) => void;
};

declare var require: any;
declare var __Root__: any;
declare var __RootProperties__: any;

export function initialize(componentUrl: string) {
    require([componentUrl], function (WebViewComponentModule: any) {
        let WebViewComponent = WebViewComponentModule.default;
        (window as any).__Root__ = ReactDOM.render(
            React.createElement(WebViewComponent, __RootProperties__),
            document.getElementById("webview_root"),
            () => notifyNative("Ready")
        );
    });
}

function notifyNative(eventName: string) {
    __WebviewListener__.notify(eventName);
}