// A '.tsx' file enables JSX support in the TypeScript compiler, 
// for more information see the following page on the TypeScript wiki:
// https://github.com/Microsoft/TypeScript/wiki/JSX

import * as React from 'react';
import * as Dependency from './dependency.js';

declare var NativeApi: any;

console.log(Dependency.x);


class App extends React.Component<null, null> {
    render() {
        return (
            <div className="App">
                <div className="App-header">
                    <h2>Welcome to React</h2>
                </div>
                { NativeApi.getItems().map((i: string)  => <div>{i}</div>) }
            </div>
        );
    }
}

export default App;