const glob = require('glob');
const path = require('path');

const entryMap = {};

glob.sync("**/*.view.tsx").forEach(f => {
    entryMap[f.replace(/^.*[\\\/]/, '').replace(/\.view.tsx$/, '')] = ['./' + f];
});

module.exports = {
    entry: entryMap,
    output: {
        path: path.join(__dirname, '/Generated'),
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
