const glob = require('glob');
const path = require('path');

const bindingsMap = {};
const entryMap = {};

/** 
 *  Get input and output entries from ts2lang file
 * */
let bindings = require('./ts2lang.json');
bindings.tasks.forEach(t => bindingsMap[t.input] = t.output);

/** 
 *  Build an entry map for webpack config
 * */
Object.keys(bindingsMap).forEach(input => {
    let output = bindingsMap[input];
    glob.sync(input).forEach(f => {

        let filePath = path.parse(f);

        // Exclude node_modules files
        if (!f.includes("node_modules")) {
            entryMap[output + filePath.name] = ['./' + f];
        }
    });
});

module.exports = {
    entry: entryMap,
    output: {
        path: __dirname,
        filename: "[name].js"
    },
    resolve: {
        extensions: [".ts", ".tsx", ".js"]
    },
    module: {
        rules: [
            { test: /\.css?$/, loader: "style-loader!css-loader" },
            { test: /\.tsx?$/, loader: "ts-loader" }
        ]
    }
};
