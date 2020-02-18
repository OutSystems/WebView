import { basename } from "path";
import { RuleSetRule } from "webpack";

// Resource files
const getResourcesRuleSet = (pluginsRelativePath? : string): RuleSetRule => {

    const ResourcesRule: RuleSetRule = {
        test: /\.(ttf|png|jpg|jpeg|bmp|gif|woff|woff2|ico|svg|html)$/,
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
                            let idx: number = url.indexOf(`/${resourceBase}/`);
                            if (idx < 0) {
                                if (pluginsRelativePath != undefined) {
                                    let pluginsStringEdited: string = pluginsRelativePath.replace(/(\.\.)/g, '');//Replace double dots ".." 
                                    pluginsStringEdited = pluginsStringEdited.replace(/[/\\/]/g, '');      //Replace backslashes "\"                              
                                    idx = url.indexOf(`/${pluginsStringEdited}/`);
                                }
                                if (idx < 0) {
                                    throw new Error("Resource not found: using a resource from another namespace without an absolute path.");
                                }
                            }
                            return url.substring(idx);
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
