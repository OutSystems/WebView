import { Configuration } from "webpack";

import getCommonConfiguration from "./Plugins/CommonConfiguration";
import DtsCleanupPlugin from "./Plugins/DtsCleanupPlugin";
import DtsGeneratorPlugin from "./Plugins/DtsGeneratorPlugin";
import { DtsFileName } from "./Plugins/Resources";
import { getCurrentDirectory } from "./Plugins/Utils";

const config = (_, argv) => {

    let standardConfig: Configuration = getCommonConfiguration("Plugins");

    // devtool
    if (argv.mode === "development") {
        standardConfig.devtool = "inline-source-map";
    }

    // Plugins
    standardConfig.plugins = standardConfig.plugins.concat(

        // DtsGeneratorPlugin
        new DtsGeneratorPlugin({ name: "", project: getCurrentDirectory(), out: DtsFileName }),

        // DtsCleanupPlugin
        new DtsCleanupPlugin([DtsFileName], [/\.d.ts$/])
    );

    return standardConfig;
};

export default config;
