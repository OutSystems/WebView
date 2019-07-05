import * as Path from "path";
import * as Webpack from "webpack";

var standardConfig: Webpack.Configuration = {
    externals: {
        'react': 'React'
    },

    entry: {
        ViewFrame: "./libs/ViewFrame.tsx"
    },

    output: {
        path: Path.resolve("."),
        filename: "ReactViewResources.js",
        library: 'ViewFrame',
        libraryTarget: 'umd',
        umdNamedDefine: true
    },

    resolve: {
        extensions: [".ts", ".tsx", ".js"]
    },

    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: [
                    {
                        loader: "ts-loader",
                        options: {
                            configFile: "libs/tsconfig.json"
                        }
                    }
                ]
            }
        ]
    }
}

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