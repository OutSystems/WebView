import { CssPlaceholder, MiniCssExtractPluginName, OutputDirectoryDefault } from "./Resources";

/**
 *  Renames entry module generated filenames.
 *  Won't be necessary when using Webpack 5.
 *  (for more info see https://github.com/webpack/webpack/issues/6598)
 *  
 * */
export default class RenameChunksPlugin {

    public apply(compiler) {

        compiler.hooks.compilation.tap("RenameChunksPlugin", (compilation) => {
            compilation.chunkTemplate.hooks.renderManifest.intercept({
                register(tapInfo) {

                    if (tapInfo.name === "JavascriptModulesPlugin") {
                        const originalMethod = tapInfo.fn;

                        tapInfo.fn = (result, options) => {

                            let filenameTemplate = "";
                            const chunk = options.chunk;
                            const outputOptions = options.outputOptions;

                            if (chunk.filenameTemplate) {
                                filenameTemplate = chunk.filenameTemplate;

                            } else if (chunk.hasEntryModule()) {

                                // JS files
                                filenameTemplate = outputOptions.filename;

                                // CSS files
                                let generatedFileName = result[0];
                                if (generatedFileName && generatedFileName.identifier.startsWith(MiniCssExtractPluginName)) {
                                    generatedFileName.filenameTemplate = OutputDirectoryDefault + CssPlaceholder;
                                }

                            } else {
                                filenameTemplate = outputOptions.chunkFilename;
                            }

                            options.chunk.filenameTemplate = filenameTemplate;
                            return originalMethod(result, options);
                        };
                    }
                    return tapInfo;
                }
            });
        });
    }
}
