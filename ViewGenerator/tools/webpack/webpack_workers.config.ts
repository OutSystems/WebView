import { sync } from "glob";
import { parse, resolve, join } from "path";
import { getCurrentDirectory } from "./Plugins/Utils";

const config = (_, argv) => {

    let entryMap = {};
    let projectDir = argv.projectDir ? argv.projectDir.replace(/\\/g, "/") : "";

    sync(join(projectDir, "**/*.worker.js")).forEach(f => {
        let entryName: string = parse(f).name;
        entryMap[entryName] = "./" + f;
    });

    return {
        entry: entryMap,
        optimization: {
            minimize: true
        },
        output: {
            globalObject: 'self',
            path: resolve(getCurrentDirectory(), "Generated")
        },
    };
};

export default config;
