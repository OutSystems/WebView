import MiniCssExtractPlugin from "mini-css-extract-plugin";
import Path from "path";
import Webpack, { Compiler } from "webpack";

const OutputFolder: string = "Generated";

class MiniCssExtractPluginCleanup {

    private patterns: RegExp[];

    constructor(patterns) {
        this.patterns = patterns;
    }

    apply(compiler: Compiler) {
        compiler.hooks.emit.tapAsync("MiniCssExtractPluginCleanup", (compilation, callback) => {
            Object.keys(compilation.assets)
                .filter(asset => this.patterns.some(p => p.test(asset)))
                .forEach(asset => {
                    delete compilation.assets[asset];
                });
            callback();
        });
    }
}

const config = (_, argv) => {

    let entryPath: string = argv.entryPath;
    let fileExtensionLen: number = entryPath.length - entryPath.lastIndexOf(".");
    let entryName: string = entryPath.slice(entryPath.lastIndexOf("\\") + 1, -fileExtensionLen);
    let entryMap: { [entry: string]: string } = {};
    entryMap[entryName] = './' + entryPath;

    var customConfig: Webpack.Configuration = {
        entry: entryMap,

        output: {
            path: Path.resolve('.'),
            filename: '[name].js.map'
        },

        resolveLoader: {
            modules: [Path.join(__dirname, '/node_modules')],
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
                }
            ]
        },

        plugins: [
            new MiniCssExtractPlugin({
                filename: OutputFolder + '/[name].css'
            }),
            new MiniCssExtractPluginCleanup([/\.js.map$/])
        ]

    }

    return customConfig;
};

export default config;
