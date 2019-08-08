import Glob from "glob";
import Path from "path";
import MiniCssExtractPlugin from "mini-css-extract-plugin";
import Webpack from "webpack";
import ManifestPlugin from "webpack-manifest-plugin";
import { existsSync } from "fs";
import { outputFileSync } from "fs-extra";

const DtsExtension = ".d.ts";
const CssExtension = ".css";
const JsExtension = ".js";

const entryMap: {
    [entry: string]: string
} = {};

const outputMap: {
    [entry: string]: string
} = {};

const namespaceMap: {
    [entry: string]: string
} = {};

const aliasMap: {
    [key: string]: string
} = {};

const externalsObjElement: Webpack.ExternalsObjectElement = {};

/** 
 *  Build entry and output mappings for webpack config
 * */
function getConfiguration(input: string, output: string, namespace: string): void {
    Glob.sync(input).forEach(f => {

        // Exclude node_modules files
        if (!f.includes("node_modules") && !f.endsWith(DtsExtension)) {

            let entryName: string = Path.parse(f).name;
            entryMap[entryName] = "./" + f;
            outputMap[entryName] = output;
            namespaceMap[entryName] = namespace;
        }
    });
}

/*
 * Generates an entry file.
 * */
function generateEntryFile(files: string[],
    entryName: string,
    extension: string,
    relativePath: string,
    namespace: string,
    entryFilter: (file: string) => boolean) {

    let fileEntries = files.filter(entryFilter).map(f => "/" + namespace + "/" + f).join("\n");
    outputFileSync(relativePath + entryName + extension + ".entry", (fileEntries || []).length > 0 ? fileEntries : "");
}

/*
 * Generates a manifest for the output.
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

        if (name) {
            var relativePath = outputMap[name];
            var namespace = namespaceMap[name];

            // CSS
            generateEntryFile(files,
                name,
                CssExtension,
                relativePath,
                namespace,
                f => f.endsWith(CssExtension));

            // JS
            generateEntryFile(files,
                name,
                JsExtension,
                relativePath,
                namespace,
                f => f.endsWith(JsExtension) && !f.endsWith("/" + name + ".js"));

            return { ...acc, [name]: files };
        }

        return acc;
    }, seed);

    return entryArrayManifest;
}

/*
 * Custom typescript error formater for Visual Studio.
 * */
function customErrorFormatter(error, colors) {
    let messageColor = error.severity === "warning" ? colors.bold.yellow : colors.bold.red;
    let errorMsg =
        colors.bold.white('(') +
        colors.bold.cyan(error.line.toString() + "," + error.character.toString()) +
        colors.bold.white(')') +
        messageColor(": " + error.severity.toString() + " " + error.code.toString() + ": ") +
        messageColor(error.content);
    return messageColor(error.file) + errorMsg;
}

/** 
 *  Get aliases and externals from a configuration file, if exists
 * */
function generateExtendedConfig(pluginsRelativePath: string): void {

    let pluginsPath: string = pluginsRelativePath.replace("\\", "\/");

    let webpackOutputConfigFile = Path.resolve(pluginsPath, "webpack-output-config.json");
    if (existsSync(webpackOutputConfigFile)) {
        let outputConfig = require(webpackOutputConfigFile);

        let allAliases = outputConfig.alias;
        if (allAliases) {
            Object.keys(allAliases).forEach(key => aliasMap[key] = Path.resolve(pluginsPath, allAliases[key]));
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
    getConfiguration(t.input, t.output, t.parameters.namespace)
);

let standardConfig: Webpack.Configuration = {
    entry: entryMap,

    externals: {
        "react": "React",
        "react-dom": "ReactDOM",
        "prop-types": "PropTypes",
        "ViewFrame": "ViewFrame"
    },

    output: {
        path: Path.resolve("."),
        filename: getOutputFileName,
        chunkFilename: "Generated/chunk_[chunkhash:8].js",
        library: ["Views", "[name]"],
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
                    "css-loader",
                    {
                        loader: "resolve-url-loader",
                        options: {
                            keepQuery: true
                        }
                    },
                    {
                        loader: "sass-loader",
                        options: {
                            sourceMap: true,
                            sourceMapContents: false
                        }
                    }
                ]
            },
            {
                test: /\.(png|jpg|jpeg|bmp|gif|woff|woff2|ico|svg)$/,
                use: [
                    {
                        loader: 'file-loader',
                        options: {
                            emitFile: false,
                            name: '[path][name].[ext]',
                            publicPath: (url: string, _, __) => {

                                // relative paths starting with ".." are replaced by "_"
                                if (url.startsWith("_")) {
                                    return url.substring(1);
                                }

                                return `/${Path.basename(Path.resolve("."))}/${url}`;
                            }
                        },
                    },
                ],
            },
            {
                test: /\.tsx?$/,
                loader: "ts-loader",
                options: {
                    errorFormatter: customErrorFormatter
                }
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

    if (argv.pluginsRelativePath) {

        generateExtendedConfig(argv.pluginsRelativePath);

        if (Object.keys(aliasMap).length > 0) {
            standardConfig.resolve.alias = aliasMap;
        }

        Object.keys(externalsObjElement).forEach(key => standardConfig.externals[key] = externalsObjElement[key]);
    }

    return standardConfig;
};

export default config;
