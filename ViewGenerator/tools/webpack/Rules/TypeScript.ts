import { RuleSetRule } from "webpack";

import { customErrorFormatter } from "../Plugins/Utils";

// .ts / .tsx  files
const TypeScriptRuleSet: RuleSetRule = {
    test: /\.tsx?$/,
    loader: "ts-loader",
    options: {
        errorFormatter: customErrorFormatter
    }
}

export default TypeScriptRuleSet;
