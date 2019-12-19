import { resolve } from "path";
import { Configuration } from "webpack";

import getCommonConfiguration from "./Plugins/CommonConfiguration";
import DtsCleanupPlugin from "./Plugins/DtsCleanupPlugin";
import DtsGeneratorPlugin from "./Plugins/DtsGeneratorPlugin";
import { DtsFileName } from "./Plugins/Resources";
import { getCurrentDirectory } from "./Plugins/Utils";

const config = (_, argv) => {

    let projectDir = argv.projectDir ? resolve(argv.projectDir)  : "";
    let standardConfig: Configuration = getCommonConfiguration("Plugins", argv.useCache, projectDir);

    // Plugins
    standardConfig.plugins = standardConfig.plugins.concat(

        // DtsGeneratorPlugin
        new DtsGeneratorPlugin({ name: "", project: projectDir || getCurrentDirectory(), out: DtsFileName }),

        // DtsCleanupPlugin
        new DtsCleanupPlugin([DtsFileName], [/\.d.ts$/])
    );

    return standardConfig;
};

export default config;
