const fs = require('fs');
const entryMap = {};

fs.readdirSync('.')
    .filter(file => file.match(/.*\.view.tsx$/))
    .forEach(f => { entryMap[f.replace(/\.view.tsx$/, '')] = ['./' + f]; });

module.exports = {
    mode: "development",
    entry: entryMap,
    output: {
        filename: "dummybundle.js"
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
