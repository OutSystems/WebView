// A '.tsx' file enables JSX support in the TypeScript compiler, 
// for more information see the following page on the TypeScript wiki:
// https://github.com/Microsoft/TypeScript/wiki/JSX
var __extends = (this && this.__extends) || function (d, b) {
    for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p];
    function __() { this.constructor = d; }
    d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
};
define(["require", "exports", "react", "./dependency.js"], function (require, exports, React, Dependency) {
    "use strict";
    console.log(Dependency.x);
    var App = (function (_super) {
        __extends(App, _super);
        function App() {
            var _this = _super.call(this) || this;
            _this.state = { items: [], i: 0 };
            return _this;
        }
        App.prototype.componentDidMount = function () {
            var _this = this;
            console.time("native");
            NativeApi.getItems().then(function (result) {
                _this.setState({ items: result, i: 1 });
                console.timeEnd("native");
            }).catch(function () {
                _this.setState({ i: 2 });
            });
        };
        App.prototype.render = function () {
            return (React.createElement("div", { className: "App" },
                React.createElement("div", { className: "App-header" },
                    React.createElement("h2", null,
                        "Welcome to React ",
                        this.state.i)),
                this.getItems()));
        };
        App.prototype.getItems = function () {
            return this.state.items.length === 0 ? React.createElement("div", null, "No items") : this.state.items.map(function (i) { return React.createElement("div", null, i.Value); });
        };
        return App;
    }(React.Component));
    Object.defineProperty(exports, "__esModule", { value: true });
    exports.default = App;
});
