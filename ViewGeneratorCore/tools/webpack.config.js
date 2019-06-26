const glob = require('glob');
const path = require('path');

const bindingsMap = {};
const entryMap = {};
const outputMap = {};

/** 
 *  Get input and output entries from ts2lang file
 * */
let bindings = require(path.resolve('./ts2lang.json'));
bindings.tasks.forEach(t => bindingsMap[t.input] = t.output);

/** 
 *  Build entry and output mappings for webpack config
 * */
Object.keys(bindingsMap).forEach(input => {
    let output = bindingsMap[input];
    glob.sync(input).forEach(f => {

        // Exclude node_modules files
        if (!f.includes("node_modules")) {

            let entryName = path.parse(f).name;
            entryMap[entryName] = ['./' + f];
            outputMap[entryName] = output;
        }
    });
});

module.exports = {

    target: 'node',

    entry: entryMap,

    externals: {
        'react': 'react',
        'react-dom': 'react-dom',
        'ViewFrame': 'ViewFrame'
    },

    output: {
        path: path.resolve('.'),
        filename: (chunkData) => path.join(outputMap[chunkData.chunk.name], "[name].js"),
        chunkFilename: "chunks/[name].js"
    },

    optimization: {
        splitChunks: {
            chunks: 'all'
        }
    },

    resolveLoader: {
        modules: [ path.join(__dirname, '/node_modules') ],
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
