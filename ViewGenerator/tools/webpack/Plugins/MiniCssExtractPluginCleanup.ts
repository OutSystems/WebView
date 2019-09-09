import { Compiler } from "webpack";

export default class MiniCssExtractPluginCleanup {

    private patterns: RegExp[];

    constructor(patterns: RegExp[]) {
        this.patterns = patterns;
    }

    public apply(compiler: Compiler) {
        compiler.hooks.emit.tapAsync("MiniCssExtractPluginCleanup", (compilation, callback) => {
            Object.keys(compilation.assets)
                .filter(asset => this.patterns.some(p => p.test(asset)))
                .forEach(asset => {
                    delete compilation.assets[asset];
                });
            callback();
        });
    }
}
