import Glob from "glob";
import Path from "path";
import Webpack from "webpack";
import ManifestPlugin, { FileDescriptor } from "webpack-manifest-plugin";

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

            let entryName: string = Path.parse(f).name.replace(".view", "");
            entryMap[entryName] = './' + f;
            outputMap[entryName] = output;
        }
    });
}

/*
 * Generate a manifest for the output.
 * */
function generateManifest(seed: object, files: ManifestPlugin.FileDescriptor[]) {
    let entries = [];

    files.forEach(f => (f.chunk["_groups"] || []).forEach(g => {
        if (entries.indexOf(g) < 0) {
            entries.push(g);
        }
    }));

    let entryArrayManifest = entries.reduce((acc, entry) => {
        let name: string = (entry.options || {}).name || (entry.runtimeChunk || {}).name;
        let files: string[] = [];
        if (entry.chunks) {
            entry.chunks.forEach(c => {
                if (c.files) {
                    files = files.concat(c.files);
                }
            });
        }
        return name ? { ...acc, [name]: files } : acc;
    }, seed);

    return entryArrayManifest;
}

// 🔨 Webpack allows strings and functions as its output configurations,
// however, webpack typings only allow strings at the moment. 🔨
let getOutputFileName: any = (chunkData) => {
    return outputMap[chunkData.chunk.name] + "[name].js";
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
            automaticNameDelimiter: '_',
            chunks: 'all',
            minSize: 1,
            cacheGroups: {
                vendors: {
                    test: /[\\/](node_modules)[\\/]/
                },
            }
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
    },

    plugins: [
        new ManifestPlugin({
            fileName: "manifest.json",
            generate: generateManifest
        })
    ]
};

const config = (_, argv) => {
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
    }
    return standardConfig;
};

export default config;
