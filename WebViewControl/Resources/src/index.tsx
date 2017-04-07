///<amd-dependency path="./webview-root-component" name="WebViewComponentModule" />
import * as React from 'react';
import * as ReactDOM from 'react-dom';

declare var WebViewComponentModule: any;
declare var NativeApi: any;

let WebViewComponent = WebViewComponentModule.default;

ReactDOM.render(
    <WebViewComponent/>,
    document.getElementById("webview_root")
);

function wrapNativeApi(api: Object) {
    debugger;
}

wrapNativeApi(NativeApi);

