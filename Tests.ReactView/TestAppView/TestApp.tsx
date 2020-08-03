import * as React from 'react';
import { ViewFrame } from "ViewFrame";
import "./Styles.scss";
import * as Image from "./imgs/image.png";

interface IAppProperties {
    event: (args: string) => void;
    propertyValue: string;
    autoShowInnerView: boolean;
}

class App extends React.Component<IAppProperties> {

    firstRenderHtml: string;
    viewIsReady: boolean;

    constructor(props: IAppProperties) {
        super(props);
        this.viewIsReady = false;
        window.addEventListener("viewready", () => this.viewIsReady = true);
        this.firstRenderHtml = this.getHtml();
    }

    renderInnerViewContainer() {
        return this.props.autoShowInnerView ? <ViewFrame name="test"/> : null;
    }

    render() {
        const uniqueTimestamp = new Date().getTime() + "" + Math.random() ;
        return (
            <div className="App">
                <div className="App-header">
                    <h2>Welcome to React</h2>
                    <img src={Image} />
                    <div>Cache timestamp: {uniqueTimestamp}</div>
                    {this.renderInnerViewContainer()}
                </div>
            </div>
        );
    }

    callEvent() {
        this.props.event("");
    }

    checkStyleSheetLoaded(stylesheetsCount: number) {
        function getText(stylesheet: CSSStyleSheet): string {
            return Array.from(stylesheet.rules).map(rule => {
                if (rule instanceof CSSImportRule) {
                    return getText(rule.styleSheet);
                } else {
                    return rule.cssText;
                }
            }).join("\n");
        }

        var intervalHandle = 0;
        intervalHandle = setInterval(() => {
            if (document.styleSheets.length >= stylesheetsCount) {
                var stylesheets = Array.from(document.styleSheets).map(s => getText(s as CSSStyleSheet)).join("\n");
                this.props.event(stylesheets);
                clearInterval(intervalHandle);
            }
        }, 50);
    }

    checkPluginModuleLoaded() {
        var intervalHandle = 0;
        intervalHandle = setInterval(() => {
            if ((window as any).PluginModuleLoaded) {
                this.props.event("PluginModuleLoaded");
            }
            clearInterval(intervalHandle);
        }, 50);
    }

    checkAliasedModuleLoaded() {
        if ((window as any).AliasedModuleLoaded) {
            this.props.event("AliasedModuleLoaded");
        }
    }

    checkViewReady() {
        var intervalHandle = 0;
        intervalHandle = setInterval(() => {
            if (this.viewIsReady) {
                this.props.event("ViewReadyTrigger");
            }
            clearInterval(intervalHandle);
        }, 50);
    }

    loadCustomResource(url: string) {
        console.log(url);
        var img = document.createElement("img");
        img.src = url;
        document.body.appendChild(img);
    }

    getFirstRenderHtml() {
        return this.firstRenderHtml;
    }

    getHtml() {
        return (document.body.firstElementChild as HTMLElement).innerHTML || "";
    }

    getPropertyValue() {
        return this.props.propertyValue;
    }

    getCurrentTime() {
        return new Date().valueOf();
    }

    getStartTime() {
        return window.performance.timing.navigationStart;
    }
}

export default App; 