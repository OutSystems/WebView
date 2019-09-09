import dtsGenerator, { DtsGeneratorOptions } from 'dts-generator';
import { Compiler } from "webpack";

export default class DtsGeneratorPlugin {

    private options: DtsGeneratorOptions;

    constructor(options: DtsGeneratorOptions) {
        this.options = options;
    }

    public apply(compiler: Compiler) {
        compiler.hooks.emit.tapAsync("DtsGeneratorPlugin", (_, callback) => {
            dtsGenerator(this.options);
            callback();
        });
    }
}
