import { basename } from "path";
import { RuleSetRule } from "webpack";

// Resource files
let getResourcesRuleSet = (projectDir: string): RuleSetRule => {

    const ResourcesRule: RuleSetRule = {
        test: /\.(png|jpg|jpeg|bmp|gif|woff|woff2|ico|svg|html)$/,
        use: [
            {
                loader: 'file-loader',
                options: {
                    emitFile: false,
                    name: '[path][name].[ext]',
                    publicPath: (url: string, _, context: string) => {
                        let processedUrl: string = url;
                        let resourceBase: string = basename(context);

                        if (projectDir) {
                            processedUrl = processedUrl.replace(`/${resourceBase}/`, `/${basename(projectDir)}/`);
                        }

                        // relative paths starting with ".." are replaced by "_"
                        if (processedUrl.startsWith("_")) {
                            return processedUrl.substring(processedUrl.indexOf(`/${resourceBase}/`));
                        }

                        return `/${resourceBase}/${processedUrl}`;
                    }
                },
            },
        ]
    };
    return ResourcesRule;
};

export default getResourcesRuleSet;
