import Glob from "glob";
import Path from "path";
import MiniCssExtractPlugin from "mini-css-extract-plugin";
import Webpack, { Module } from "webpack";
import ManifestPlugin, { Chunk } from "webpack-manifest-plugin";

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

/*
 * Generate a name for a chunk.
 * */
function generateChunkName(_module: Module, chunks: Chunk[], cacheGroupKey: string) {
    const allChunksNames = chunks.map(item => item.name.replace(".view", "").replace(".", "_")).join('_');
    return cacheGroupKey === "default" ? allChunksNames : cacheGroupKey + "_" + allChunksNames;
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
            chunks: 'all',
            minSize: 1,
            name: generateChunkName,
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
            {
                test: /\.(sa|sc|c)ss$/,
                use: [
                    {
                        loader: MiniCssExtractPlugin.loader,
                        options: {
                            hmr: false
                        }
                    },
                    'css-loader',
                    'sass-loader'
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
            filename: 'Generated/[name].css',
            chunkFilename: 'Generated/[name].css'
        }),
        new ManifestPlugin({
            fileName: "manifest.json",
            generate: generateManifest
        })
    ]
};

// Current webpack typings do not recognize automaticNameMaxLength option.
// Default is 30 characters, so we need to increase this value.
(standardConfig.optimization.splitChunks as any).automaticNameMaxLength = 100;

const config = (_, argv) => {
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
    }
    return standardConfig;
};

export default config;
