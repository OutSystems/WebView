import { existsSync } from "fs";
import { resolve } from "path";
import { Configuration } from "webpack";

import getCommonConfiguration from "./Plugins/CommonConfiguration";
import { Dictionary } from "./Plugins/Utils";

const config = (_, argv) => {

    let aliasMap: Dictionary<string> = {};
    let externalsMap: Dictionary<string> = {};

    // Get aliases and externals from a configuration file, if exists
    let generateExtendedConfig = (pluginsRelativePath: string): void => {
        let pluginsPath: string = pluginsRelativePath.replace(/\\/g, "/")

        let webpackOutputConfigFile = resolve(pluginsPath, "webpack-output-config.json");
        if (existsSync(webpackOutputConfigFile)) {
            let outputConfig = require(webpackOutputConfigFile);

            // Aliases
            let allAliases = outputConfig.alias;
            if (allAliases) {
                Object.keys(allAliases).forEach(key => aliasMap[key] = resolve(pluginsPath, allAliases[key]));
            }

            // Externals
            externalsMap = outputConfig.externals || {};

        } else {
            throw new Error("Extended configuration file not found.");
        }
    };

    let tsConfigFile = argv.tsConfigFile ? resolve(argv.tsConfigFile) : "";
    let projectDir = argv.projectDir ? resolve(argv.projectDir) : "";
    let standardConfig: Configuration = getCommonConfiguration("Views", argv.useCache, projectDir, tsConfigFile);

    // SplitChunksOptions
    standardConfig.optimization.splitChunks = {
        chunks: "all",
        minSize: 1,
        cacheGroups: {
            vendors: {
                test: /[\\/](node_modules)[\\/]/
            }
        }
    };

    // SplitChunksOptions.automaticNameMaxLength
    //
    // Current webpack typings do not recognize automaticNameMaxLength option.
    // Default is 30 characters, so we need to increase this value.
    (standardConfig.optimization.splitChunks as any).automaticNameMaxLength = 250;

    if (argv.pluginsRelativePath) {
        generateExtendedConfig(argv.pluginsRelativePath);

        // resolve.alias
        if (Object.keys(aliasMap).length > 0) {
            standardConfig.resolve.alias = aliasMap;
        }

        // externals
        if (Object.keys(externalsMap).length > 0) {
            Object.keys(externalsMap).forEach(key => standardConfig.externals[key] = externalsMap[key]);
        }
    }

    return standardConfig;
};

export default config;
