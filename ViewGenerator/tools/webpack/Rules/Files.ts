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

                        // URL can be one of the following:
                        //
                        // - 1) A relative path to a resource in another assembly, e.g. "_/MyOtherAssembly/Path/to/Resource.png"
                        // - 2) A relative path to a resource in the assembly itself, e.g. "Path/to/Resource.png"
                        // - 3) An absolute path to a resource in another assembly, e.g. "Z:/MyOtherAssembly/Path/to/Resource.png"
                        // - 4) An absolute path to a resource in assembly itself, e.g. "Z:/MyAssembly/Path/to/Resource.png"
                        //
                        // Context represents the path of the project being built by webpack, e.g. "C:\Git\Path\to\Project\"
                        //
                        let resourceBase: string = basename(context);

                        let idx: number = url.indexOf(`/${resourceBase}/`);
                        if (idx < 0) {
                            if (pluginsRelativePath != undefined) {
                                let pluginsStringEdited: string = pluginsRelativePath.replace(/(\.\.)/g, ''); //Replace double dots ".." 
                                pluginsStringEdited = pluginsStringEdited.replace(/[/\\/]/g, ''); //Replace backslashes "\"                              
                                idx = url.indexOf(`/${pluginsStringEdited}/`);
                            }
                        }

                        // relative paths starting with ".." are replaced by "_"
                        if (url.startsWith("_")) {
                            if (idx < 0) {
                                // URL is a relative path and we did not find the assembly or plugin assembly in its content
                                throw new Error("Resource not found: using a resource from another namespace without an absolute path.");
                            }

                            // URL is a relative path and contains the assembly or plugin assembly in its content
                            return url.substring(idx);
                        }

                        if (url.substring(1).startsWith(":/")) {
                            // URL is an absolute path and we did not find the assembly or plugin assembly in its content
                            throw new Error("Resource not found: using a resource from another namespace without an absolute path.");
                        }

                        if (idx >= 0) {
                            // URL is an absolute path and contains the assembly or plugin assembly in its content
                            return url.substring(idx);
                        }

                        // URL is a relative path and contains the assembly in its content
                        return `/${resourceBase}/${url}`;
                    }
                },
            },
        ]
    };
    return ResourcesRule;
};

export default getResourcesRuleSet;
