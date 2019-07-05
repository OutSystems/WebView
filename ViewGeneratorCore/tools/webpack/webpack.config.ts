import * as Glob from "glob";
import * as Path from "path";
import * as Webpack from "webpack";

const entryMap: {
    [entry: string]: string
} = {};

const outputMap: {
    [entry: string]: string
} = {};

/** 
 *  Build entry and output mappings for webpack config
 * */
function getConfiguration(input: string, output: string): void {
    Glob.sync(input).forEach(f => {

        // Exclude node_modules files
        if (!f.includes("node_modules")) {

            let entryName: string = Path.parse(f).name;
            entryMap[entryName] = './' + f;
            outputMap[entryName] = output;
        }
    });
}

// 🔨 Webpack allows strings and functions as its output configurations,
// however, webpack typings only allow strings at the moment. 🔨
let getOutputFileName: any = (chunkData) => {
    return Path.join(outputMap[chunkData.chunk.name], "[name].js");
}

/** 
 *  Get input and output entries from ts2lang file
 * */
require(Path.resolve('./ts2lang.json')).tasks.forEach(t =>
    getConfiguration(t.input, t.output)
);

var standardConfig: Webpack.Configuration = {
    entry: entryMap,

    externals: {
        'react': 'React',
        'react-dom': 'ReactDOM',
        'ViewFrame': 'ViewFrame'
    },

    output: {
        path: Path.resolve('.'),
        filename: getOutputFileName,
        chunkFilename: "Generated/[name].js",
        library: "Bundle",
        libraryTarget: 'umd',
        umdNamedDefine: true,
        globalObject: 'window'
    },

    optimization: {
        splitChunks: {
            chunks: 'all'
        }
    },

    resolveLoader: {
        modules: [Path.join(__dirname, '/node_modules')],
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

const config = (_, argv) => {
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
        standardConfig.optimization = {
            minimize: false,
        }
    }
    return standardConfig;
};

export default config;
