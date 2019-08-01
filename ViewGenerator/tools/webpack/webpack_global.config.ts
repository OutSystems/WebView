import Glob from "glob";
import Path from "path";
import MiniCssExtractPlugin from "mini-css-extract-plugin";
import Webpack from "webpack";
import ManifestPlugin from "webpack-manifest-plugin";
import { existsSync } from "fs";

const DtsExtension = ".d.ts";

const entryMap: {
    [entry: string]: string
} = {};

const outputMap: {
    [entry: string]: string
} = {};

const aliasMap: {
    [key: string]: string
} = {};

const externalsObjElement: Webpack.ExternalsObjectElement = {};

/** 
 *  Build entry and output mappings for webpack config
 * */
function getConfiguration(input: string, output: string): void {
    Glob.sync(input).forEach(f => {

        // Exclude node_modules files
        if (!f.includes("node_modules") && !f.endsWith(DtsExtension)) {

            let entryName: string = Path.parse(f).name;
            entryMap[entryName] = "./" + f;
            outputMap[entryName] = output;
        }
    });
}

/*
 * Generate a manifest for the output.
 * */
function generateManifest(seed: object, files: ManifestPlugin.FileDescriptor[]) {
    let entries = [];

    files.forEach(f => {
        if (f.chunk) {
            (f.chunk["_groups"] || []).forEach(g => {
                if (entries.indexOf(g) < 0) {
                    entries.push(g);
                }
            });
        }
    });

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
require(Path.resolve("./ts2lang.json")).tasks.forEach(t =>
    getConfiguration(t.input, t.output)
);

/** 
 *  Get aliases and externals from a configuration file, if exists
 * */
let webpackOutputConfigFile = Path.resolve("./webpack-output-config.json");
if (existsSync(webpackOutputConfigFile)) {
    let outputConfig = require(webpackOutputConfigFile);

    let allAliases = outputConfig.alias;
    if (allAliases) {
        Object.keys(allAliases).forEach(key => aliasMap[key] = Path.resolve(".", allAliases[key]));
    }

    let allExternals = outputConfig.externals;
    if (allExternals) {
        Object.keys(allExternals).forEach(key => {

            let library: string[] = allExternals[key];
            let entry: string = library[library.length - 1];
            let record: Record<string, string> = {};

            record["commonjs"] = entry;
            record["commonjs2"] = entry;
            record["root"] = allExternals[key];

            externalsObjElement[key] = record;
        });
    }
}

let standardConfig: Webpack.Configuration = {
    entry: entryMap,

    externals: {
        "react": "React",
        "react-dom": "ReactDOM",
        "prop-types": "PropTypes",
        "PluginsProvider": "PluginsProvider",
        "ViewFrame": "ViewFrame"
    },

    output: {
        path: Path.resolve("."),
        filename: getOutputFileName,
        chunkFilename: "Generated/chunk_[chunkhash:8].js",
        library: [ "Bundle" , "[name]" ],
        libraryTarget: "umd",
        umdNamedDefine: true,
        globalObject: "window"
    },

    optimization: {
        splitChunks: {
            chunks: "all",
            minSize: 1,
            cacheGroups: {
                vendors: {
                    test: /[\\/](node_modules)[\\/]/
                },
            }
        }
    },

    resolveLoader: {
        modules: [Path.join(__dirname, "/node_modules")],
    },

    resolve: {
        extensions: [".ts", ".tsx", ".js"]
    },

    module: {
        rules: [
            {
                test: /\.(sa|sc|c)ss$/,
                use: [
                    {
                        loader: MiniCssExtractPlugin.loader,
                        options: {
                            hmr: false
                        }
                    },
                    {
                        loader: "css-loader",
                        options: {
                            url: false
                        }
                    },
                    "sass-loader"
                ]
            },
            {
                test: /\.tsx?$/,
                loader: "ts-loader"
            }
        ]
    },

    plugins: [
        new MiniCssExtractPlugin({
            filename: "Generated/[name].css",
            chunkFilename: "Generated/chunk_[chunkhash:8].css"
        }),
        new ManifestPlugin({
            fileName: "manifest.json",
            generate: generateManifest
        })
    ]
};

// Current webpack typings do not recognize automaticNameMaxLength option.
// Default is 30 characters, so we need to increase this value.
(standardConfig.optimization.splitChunks as any).automaticNameMaxLength = 250;

const config = (_, argv) => {
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
    }

    if (Object.keys(aliasMap).length > 0) {
        standardConfig.resolve.alias = aliasMap;
    }

    Object.keys(externalsObjElement).forEach(key => standardConfig.externals[key] = externalsObjElement[key]);

    return standardConfig;
};

export default config;
