import * as React from 'react';

interface IAppProperties {
    event: (args: string) => void;
}

class App extends React.Component<IAppProperties, {}> {

    constructor() {
        super();
    }

    componentDidMount() {
        
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
}

export default App; 