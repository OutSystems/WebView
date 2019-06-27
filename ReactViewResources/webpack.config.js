const path = require('path');

module.exports = {

    target: 'node',

    entry: {
        'react-view-resources': [
            './node_modules/react/umd/react.production.min.js',
            './node_modules/react-dom/umd/react-dom.production.min.js',
            './node_modules/prop-types/prop-types.min.js',
            './libs/ViewFrame.tsx'
        ]
    },

    output: {
        path: path.resolve('.'),
        filename: "ReactViewResources.js"
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
};
