import { Compiler } from "webpack";

export default class DtsCleanupPlugin {

    private exclusions: string[];
    private patterns: RegExp[];

    constructor(exclusions: string[], patterns: RegExp[]) {
        this.exclusions = exclusions;
        this.patterns = patterns;
    }

    public apply(compiler: Compiler) {
        compiler.hooks.emit.tapAsync("DtsCleanupPlugin", (compilation, callback) => {
            Object.keys(compilation.assets)
                .filter(asset => this.exclusions.indexOf(asset) < 0 && this.patterns.some(p => p.test(asset)))
                .forEach(asset => {
                    delete compilation.assets[asset];
                });
            callback();
        });
    }
}
