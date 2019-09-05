import MiniCssExtractPlugin from "mini-css-extract-plugin";
import { join } from "path";
import { Configuration } from "webpack";

import MiniCssExtractPluginCleanup from "./Plugins/MiniCssExtractPluginCleanup";
import { CssPlaceholder, JsMapPlaceholder, OutputDirectoryDefault } from "./Plugins/Resources";
import { Dictionary, getCurrentDirectory } from "./Plugins/Utils"

import ResourcesRuleSet from "./Rules/Files";
import SassRuleSet from "./Rules/Sass";

const config = (_, argv) => {

    let entryPath: string = argv.entryPath; // should be only one path
    let fileExtensionLen: number = entryPath.length - entryPath.lastIndexOf(".");
    let entryName: string = entryPath.slice(entryPath.lastIndexOf("\\") + 1, -fileExtensionLen);
    let entryMap: Dictionary<string> = {};

    entryMap[entryName] = './' + entryPath;

    let stylesheetsConfig: Configuration = {
        entry: entryMap,

        output: {
            path: getCurrentDirectory(),
            filename: JsMapPlaceholder
        },

        resolveLoader: {
            modules: [ join(__dirname, "/node_modules") ],
        },

        module: {
            rules: [
                SassRuleSet,
                ResourcesRuleSet
            ]
        },

        plugins: [
            new MiniCssExtractPlugin({ filename: OutputDirectoryDefault + CssPlaceholder }),
            new MiniCssExtractPluginCleanup([/\.js.map$/])
        ]
    }

    return stylesheetsConfig;
};

export default config;
