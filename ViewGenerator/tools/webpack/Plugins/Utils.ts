import { outputFileSync } from "fs-extra";
import { resolve } from "path";
import { FileDescriptor } from "webpack-manifest-plugin";

import { CssExtension, EntryExtension, JsExtension, JsPlaceholder, OutputDirectoryDefault } from "./Resources";

export type Dictionary<T> = { [key: string]: T };

/*
 * Generates an entry file.
 * */
function generateEntryFile(
    files: string[],
    entryName: string,
    extension: string,
    relativePath: string,
    namespace: string,
    entryFilter: (file: string) => boolean) {

    let fileEntries = files.filter(entryFilter).map(f => "/" + namespace + "/" + f).join("\n");
    outputFileSync(relativePath + entryName + extension + EntryExtension, (fileEntries || []).length > 0 ? fileEntries : "");
}

/*
 * Generates a manifest for the output.
 * */
export function generateManifest(
    seed: object,
    files: FileDescriptor[],
    relativePaths: Dictionary<string>,
    namespaces: Dictionary<string>) {

    let entries = [];

    files.forEach(f => {
        if (f.chunk) {
            (f.chunk["_groups"] || []).forEach(g => {
                if (entries.indexOf(g) < 0) {
                    entries.push(g);
                }
            });
        }
    });

    let entryArrayManifest = entries.reduce((acc, entry) => {
        let name: string = (entry.options || {}).name || (entry.runtimeChunk || {}).name;
        let files: string[] = [];
        if (entry.chunks) {
            entry.chunks.forEach(c => {
                if (c.files) {
                    files = files.concat(c.files);
                }
            });
        }
        if (name) {
            var relativePath = relativePaths[name];
            var namespace = namespaces[name];

            // CSS
            generateEntryFile(files,
                name,
                CssExtension,
                relativePath,
                namespace,
                f => f.endsWith(CssExtension));

            // JS
            generateEntryFile(files,
                name,
                JsExtension,
                relativePath,
                namespace,
                f => f.endsWith(JsExtension) && !f.endsWith("/" + name + JsExtension));

            return { ...acc, [name]: files };
        }

        return acc;
    }, seed);

    return entryArrayManifest;
}

/*
 * Custom typescript error formater for Visual Studio.
 * */
export function customErrorFormatter(error, colors) {
    let messageColor = error.severity === "warning" ? colors.bold.yellow : colors.bold.red;
    let errorMsg =
        colors.bold.white('(') +
        colors.bold.cyan(error.line.toString() + "," + error.character.toString()) +
        colors.bold.white(')') +
        messageColor(": " + error.severity.toString() + " " + error.code.toString() + ": ") +
        messageColor(error.content);

    return messageColor(error.file) + errorMsg;
}

/*
 * Gets the current directory.
 * */
export function getCurrentDirectory() {
    return resolve(".");
}

/*
 * Gets the filename from an array.
 * */
export function getFileName(relativePaths: Dictionary<string>, chunkData: any) {
    return (relativePaths[chunkData.chunk.name] || OutputDirectoryDefault) + JsPlaceholder;
}
