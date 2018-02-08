import * as React from "react";
import * as ReactDOM from "react-dom";

declare var __WebviewListener__: {
    notify: (eventName: string) => void;
};

declare var require: any;
declare var __Root__: any;
declare var __RootProperties__: any;

export function initialize(componentUrl: string) {
    (window as any).showErrorMessage = showErrorMessage;

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

function showErrorMessage(msg: string) {
    const ContainerId = "webview_error";
    let msgContainer: HTMLDivElement;
    msgContainer = document.getElementById(ContainerId) as HTMLDivElement;
    if (!msgContainer) {
        msgContainer = document.createElement("div");
        msgContainer.id = ContainerId;
        msgContainer.style.backgroundColor = "#f45642";
        msgContainer.style.color = "white";
        msgContainer.style.fontFamily = "Arial";
        msgContainer.style.fontWeight = "bold";
        msgContainer.style.fontSize = "10px"
        msgContainer.style.padding = "3px";
        msgContainer.style.position = "absolute";
        msgContainer.style.top = "0";
        msgContainer.style.left = "0";
        msgContainer.style.right = "0";
        document.body.appendChild(msgContainer);
    }
    msgContainer.innerText = msg;   
}
