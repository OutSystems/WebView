import MiniCssExtractPlugin from "mini-css-extract-plugin";
import { RuleSetRule } from "webpack";

// .sass / .scss / .css files
const SassRuleSet: RuleSetRule = {
    test: /\.(sa|sc|c)ss$/,
    use: [
        {
            loader: MiniCssExtractPlugin.loader,
            options: {
                hmr: false
            }
        },
        "css-loader",
        {
            loader: "resolve-url-loader",
            options: {
                keepQuery: true
            }
        },
        {
            loader: "sass-loader",
            options: {
                sourceMap: true,
                sourceMapContents: false
            }
        }
    ]
};

export default SassRuleSet;