import dtsGenerator, { DtsGeneratorOptions } from 'dts-generator';
import Glob from "glob";
import Path from "path";
import MiniCssExtractPlugin from "mini-css-extract-plugin";
import Webpack, { Compiler } from "webpack";
import ManifestPlugin from "webpack-manifest-plugin";

const DtsExtension = ".d.ts";

const entriesArr: string[] = [];


class DtsGeneratorPlugin {

    private options: DtsGeneratorOptions;

    constructor(options: DtsGeneratorOptions) {
        this.options = options;
    }

    apply(compiler: Compiler) {
        compiler.hooks.emit.tapAsync("DtsGeneratorPlugin", (_, callback) => {
            dtsGenerator(this.options);
            callback();
        });
    }
}

class DtsCleanupPlugin {

    private exclusions: string[];
    private patterns: RegExp[];

    constructor(exclusions, patterns) {
        this.exclusions = exclusions;
        this.patterns = patterns;
    }

    apply(compiler) {
        compiler.hooks.emit.tapAsync("DtsCleanupPlugin", (compilation, callback) => {
            Object.keys(compilation.assets)
                .filter(asset => this.exclusions.indexOf(asset) < 0 && this.patterns.some(p => p.test(asset)))
                .forEach(asset => {
                    delete compilation.assets[asset];
                });
            callback();
        });
    }
}

/** 
 *  Get entries for webpack config
 * */
function getConfiguration(input: string): void {
    Glob.sync(input).forEach(f => {

        // Exclude node_modules and d.ts files
        if (!f.includes("node_modules") && !f.endsWith(DtsExtension)) {
            entriesArr.push("./" + f);
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

/** 
 *  Get input and output entries from ts2lang file
 * */
require(Path.resolve("./ts2lang.json")).tasks.forEach(t =>
    getConfiguration(t.input)
);

var standardConfig: Webpack.Configuration = {
    entry: {
        Framework: entriesArr
    },

    externals: {
        "react": "React",
        "react-dom": "ReactDOM",
        "prop-types": "PropTypes",
        "ViewFrame": "ViewFrame"
    },

    output: {
        path: Path.resolve("."),
        filename: "Generated/Framework.js",
        library: "Bundle",
        libraryTarget: "umd",
        umdNamedDefine: true,
        globalObject: "window"
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
        }),
        new DtsGeneratorPlugin({
            name: "",
            project: Path.resolve("."),
            out: "Generated/Framework.d.ts"
        }),
        new DtsCleanupPlugin(
            ["Generated/Framework.d.ts", "Framework.d.ts"],
            [/\.d.ts$/]
        )
    ]
};

const config = (_, argv) => {
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
    }

    return standardConfig;
};

export default config;
