define(["require", "exports", "./webview-root-component", "react", "react-dom"], function (require, exports, WebViewComponentModule, React, ReactDOM) {
    "use strict";
    var WebViewComponent = WebViewComponentModule.default;
    ReactDOM.render(React.createElement(WebViewComponent, null), document.getElementById("webview_root"));
});
