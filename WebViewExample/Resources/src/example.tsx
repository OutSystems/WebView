// A '.tsx' file enables JSX support in the TypeScript compiler, 
// for more information see the following page on the TypeScript wiki:
// https://github.com/Microsoft/TypeScript/wiki/JSX

import * as React from 'react';
import * as Dependency from './dependency.js';

declare var NativeApi: any;

console.log(Dependency.x);


class App extends React.Component<null, { items: any[], i: number }> {

    constructor() {
        super();
        this.state = { items: [], i: 0 };
        
    }

    componentDidMount() {
        console.time("native");
        NativeApi.getItems().then((result: any) => {
            this.setState({ items: result, i: 1 });
            console.timeEnd("native");
        }).catch(() => {
            this.setState({ i: 2 });
        });
    }

    render() {
        return (
            <div className="App">
                <div className="App-header">
                    <h2>Welcome to React {this.state.i}</h2>
                </div>
                {this.getItems()}
            </div>
        );
    }

    getItems() {
        return this.state.items.length === 0 ? <div>No items</div> : this.state.items.map((i: any) => <div>{i.Value}</div>);
    }
}

export default App;