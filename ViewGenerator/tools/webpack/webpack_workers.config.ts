import { sync } from "glob";
import { parse, resolve } from "path";
import { getCurrentDirectory } from "./Plugins/Utils";

const config = (_, __) => {

    let entryMap = {};

    sync("**/*.worker.js").forEach(f => {
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
