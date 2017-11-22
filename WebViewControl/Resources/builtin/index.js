define(["require", "exports", "react", "react-dom"], function (require, exports, React, ReactDOM) {
    "use strict";
    Object.defineProperty(exports, "__esModule", { value: true });
    function initialize(componentUrl) {
        require([componentUrl], function (WebViewComponentModule) {
            let WebViewComponent = WebViewComponentModule.default;
            ReactDOM.render(React.createElement(WebViewComponent, null), document.getElementById("webview_root"));
        });
    }
    exports.initialize = initialize;
    function wrapNativeApi(api) {
        for (var member in api) {
            if (api.hasOwnProperty(member) && api[member] instanceof Function) {
                api[member] = new Proxy(api[member], {
                    apply: function (target, thisArg, argumentsList) {
                        if (argumentsList.length > 0) {
                            var trackCode = argumentsList[0]["TrackCode"];
                            if (trackCode !== undefined) {
                                console.log(trackCode);
                                argumentsList[0] = trackCode;
                            }
                        }
                        return target.apply(thisArg, argumentsList);
                    }
                });
            }
        }
    }
    if (typeof NativeApi !== undefined) {
        wrapNativeApi(NativeApi);
    }
});
