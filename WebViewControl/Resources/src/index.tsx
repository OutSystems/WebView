import * as React from 'react';
import * as ReactDOM from 'react-dom';

declare var NativeApi: any;
declare var require: any;

export function initialize(componentUrl: string) {
    require([componentUrl], function (WebViewComponentModule: any) {
        let WebViewComponent = WebViewComponentModule.default;
        ReactDOM.render(
            <WebViewComponent />,
            document.getElementById("webview_root")
        );
    });
}

/*function wrapNativeApi(api: Object) {
    debugger;
}

if (typeof NativeApi !== undefined) {
    wrapNativeApi(NativeApi);
}*/
