define(["require", "exports", "react", "react-dom"], function (require, exports, React, ReactDOM) {
    "use strict";
    Object.defineProperty(exports, "__esModule", { value: true });
    function initialize(componentUrl) {
        require([componentUrl], function (WebViewComponentModule) {
            var WebViewComponent = WebViewComponentModule.default;
            ReactDOM.render(React.createElement(WebViewComponent, null), document.getElementById("webview_root"));
        });
    }
    exports.initialize = initialize;
});
/*function wrapNativeApi(api: Object) {
    debugger;
}

if (typeof NativeApi !== undefined) {
    wrapNativeApi(NativeApi);
}*/
