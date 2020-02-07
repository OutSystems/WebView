import ForkTsCheckerWebpackPlugin from "fork-ts-checker-webpack-plugin";
import MiniCssExtractPlugin from "mini-css-extract-plugin";
import { sync } from "glob";
import { join, parse, resolve } from "path";
import { Configuration } from "webpack";
import ManifestPlugin from "webpack-manifest-plugin";

// Plugins / Resources
import RenameChunksPlugin from "./RenameChunksPlugin";
import { CssPlaceholder, CssChunkPlaceholder, DtsExtension, OutputDirectoryDefault, JsChunkPlaceholder, NamePlaceholder } from "./Resources";
import { Dictionary, customErrorFormatter, generateManifest, getCurrentDirectory, getFileName, getPublicPath } from "./Utils";

// Rules
import getResourcesRuleSet from "../Rules/Files";
import SassRuleSet from "../Rules/Sass";
import getTypeScriptRuleSet from "../Rules/TypeScript";

let getCommonConfiguration = (libraryName: string, useCache: boolean): Configuration => {

    const entryMap: Dictionary<string> = {}
    const outputMap: Dictionary<string> = {};
    const namespaceMap: Dictionary<string> = {};

    // Build entry, output, and namespace mappings for webpack config
    let getConfiguration = (input: string, output: string, namespace: string): void => {
        sync(input).forEach(f => {

            // Exclude node_modules and d.ts files
            if (!f.includes("node_modules") && !f.endsWith(DtsExtension)) {

                let entryName: string = parse(f).name;
                entryMap[entryName] = "./" + f;
                outputMap[entryName] = output;
                namespaceMap[entryName] = namespace;
            }
        });
    }

    // Gets input and output entries from ts2lang file
    require(resolve("./ts2lang.json")).tasks.forEach(t => getConfiguration(t.input, t.output, t.parameters.namespace));

    // 🔨 Webpack allows strings and functions as its output configurations,
    // however, webpack typings only allow strings at the moment. 🔨
    let getOutputFileName: any = (chunkData) => getFileName(outputMap, chunkData);

    let currentDirectory: string = getCurrentDirectory();
    let assemblyPublicPath = getPublicPath();
    const Configuration: Configuration = {

        entry: entryMap,

        externals: {
            "react": "React",
            "react-dom": "ReactDOM",
            "prop-types": "PropTypes",
            "ViewFrame": "ViewFrame",
            "PluginsProvider": "PluginsProvider",
            "ResourceLoader": "ResourceLoader"
        },

        output: {
            path: currentDirectory,
            filename: getOutputFileName,
            chunkFilename: OutputDirectoryDefault + JsChunkPlaceholder,
            library: [libraryName, NamePlaceholder],
            libraryTarget: "window",
            globalObject: "window",
            devtoolNamespace: libraryName,
            publicPath: assemblyPublicPath
        },

        optimization: {
            runtimeChunk: {
                name: "Runtime"
            }
        },

        node: false,

        resolveLoader: {
            modules: [join(__dirname, "/../node_modules")],
        },

        resolve: {
            extensions: [".ts", ".tsx", ".js"]
        },

        module: {
            rules: [
                SassRuleSet,
                getResourcesRuleSet(),
                getTypeScriptRuleSet(useCache)
            ]
        },

        plugins: [
            new ForkTsCheckerWebpackPlugin({
                checkSyntacticErrors: true,
                formatter: (msg, useColors) => customErrorFormatter(msg, useColors, currentDirectory),
                measureCompilationTime: true
            }),

            new RenameChunksPlugin(),

            new MiniCssExtractPlugin({
                filename: OutputDirectoryDefault + CssPlaceholder,
                chunkFilename: OutputDirectoryDefault + CssChunkPlaceholder
            }),

            new ManifestPlugin({
                fileName: "manifest.json",
                generate: (seed, files) => generateManifest(seed, files, outputMap, namespaceMap)
            })
        ]
    };

    return Configuration;
};

export default getCommonConfiguration;
