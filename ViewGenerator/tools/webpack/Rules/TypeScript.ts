import { cpus } from "os";
import { join } from "path";
import { RuleSetRule } from "webpack";

import { CacheDirectoryDefault } from "../Plugins/Resources";
import { getCurrentDirectory } from "../Plugins/Utils";

// .ts / .tsx  files
const TypeScriptRuleSet: RuleSetRule = {
    test: /\.tsx?$/,
    use: [
        {
            loader: "cache-loader",
            options: {
                cacheDirectory: join(getCurrentDirectory(), CacheDirectoryDefault) 
            }
        },
        {
            loader: "thread-loader",
            options: {
                // There should be 1 CPU available for the fork-ts-checker-webpack-plugin
                workers: cpus().length - 1
            }
        },
        {
            loader: "ts-loader",
            options: {
                happyPackMode: true
            }
        }
    ]
}

export default TypeScriptRuleSet;
