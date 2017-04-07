// A '.tsx' file enables JSX support in the TypeScript compiler, 
// for more information see the following page on the TypeScript wiki:
// https://github.com/Microsoft/TypeScript/wiki/JSX

import * as React from 'react';
import * as Dependency from './dependency.js';

declare var NativeApi: INativeApi;

console.log(Dependency.x);


class App extends React.Component<null, { items: any[], i: number }> {

    constructor() {
        super();
        this.state = { items: [], i: 0 };
        
    }

    componentDidMount() {
        console.time("native");
        NativeApi.getItems().then((result) => {
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
                {this.renderItems()}
            </div>
        );
    }

    onItemClick(item: IJsObj, value: string) {
        item.Value = value;
        NativeApi.executeOnItem(item.TrackCode, value);
    }

    renderItem(item: IJsObj) {
        let input: HTMLInputElement;
        return <div><input defaultValue={item.Value} ref={(e) => input = e} /><button onClick={() => this.onItemClick(item, input.value)}>Update</button></div>;
    }

    renderItems() {
        return this.state.items.length === 0 ? <div>No items</div> : this.state.items.map((item: any) => this.renderItem(item));
    }
}

export default App;