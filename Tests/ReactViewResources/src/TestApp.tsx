import * as React from 'react';
import "css!styles/styles.css";

interface IAppProperties {
    event: (args: string) => void;
}

class App extends React.Component<IAppProperties, {}> {

    constructor() {
        super();
    }

    render() {
        return (
            <div className="App">
                <div className="App-header">
                    <h2>Welcome to React</h2>
                    <img src="imgs/image.png" />
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

    loadCustomResource(url: string) {
        console.log(url);
        var img = document.createElement("img");
        img.src = url;
        document.body.appendChild(img);
    }
}

export default App; 