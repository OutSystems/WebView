import * as React from 'react';
import "css!../css/styles.css";

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
                </div>
            </div>
        );
    }

    callEvent() {
        this.props.event("");
    }

    checkStyleSheetLoaded() {
        var intervalHandle = 0;
        intervalHandle = setInterval(() => {
            if (document.styleSheets.length > 0) {
                var stylesheets = Array.from(document.styleSheets).map(s => Array.from((s as CSSStyleSheet).rules).map(r => r.cssText).join("\n")).join("\n");
                this.props.event(stylesheets);
                clearInterval(intervalHandle);
            }
        }, 50);
    }
}

export default App; 