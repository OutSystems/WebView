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
                this.props.event((document.styleSheets[0] as CSSStyleSheet).rules[0].cssText);
                clearInterval(intervalHandle);
            }
        }, 50);
    }
}

export default App; 