import { basename } from "path";
import { RuleSetRule } from "webpack";

// Resource files
const getResourcesRuleSet = (): RuleSetRule => {

    const ResourcesRule: RuleSetRule = {
        test: /\.(png|jpg|jpeg|bmp|gif|woff|woff2|ico|svg|html)$/,
        use: [
            {
                loader: 'file-loader',
                options: {
                    emitFile: false,
                    name: '[path][name].[ext]',
                    publicPath: (url: string, _, context: string) => {
                        let resourceBase: string = basename(context);

                        // relative paths starting with ".." are replaced by "_"
                        if (url.startsWith("_")) {
                            return url.substring(url.indexOf(`/${resourceBase}/`));
                        }

                        return `/${resourceBase}/${url}`;
                    }
                },
            },
        ]
    };
    return ResourcesRule;
};

export default getResourcesRuleSet;
