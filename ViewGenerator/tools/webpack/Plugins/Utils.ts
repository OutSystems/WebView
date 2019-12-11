import Chalk from "chalk";
import { outputFileSync } from "fs-extra";
import { resolve, sep as pathSeparatator } from "path";
import { NormalizedMessage } from "fork-ts-checker-webpack-plugin/lib/NormalizedMessage";
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
export function customErrorFormatter(message: NormalizedMessage, enableColors: boolean, namespace: string) {
    const colors = Chalk.constructor({ enabled: enableColors });
    const defaultSeverity = "error";
    const defaultColor = colors.bold.red;
    const locationColor = colors.bold.cyan;
    const codeColor = colors.grey;

    if (message.file && message.line && message.character) {

        // e.g. file.ts(17,20): error TS0168: The variable 'foo' is declared but never used.
        return locationColor(message.file + "(" + message.line + "," + message.character + ")") +
            defaultColor(":") + " " +
            defaultColor(defaultSeverity.toUpperCase()) + " " +
            codeColor("TS" + message.code) +
            defaultColor(":") + " " +
            defaultColor(message.content);
    }

    if (!message.file) {
        // some messages do not have file specified, although logger needs it
        (message as any).file = namespace;
    }

    // e.g. error TS6053: File 'file.ts' not found.
    return defaultColor(defaultSeverity.toUpperCase()) + " " +
        codeColor("TS" + message.code) +
        defaultColor(":") + " " +
        defaultColor(message.content);
}

/*
 * Gets the current directory.
 * */
export function getCurrentDirectory() {
    return resolve(".");
}

export function getPublicPath() {
    let currentPath = resolve(".");
    let segments = currentPath.split(pathSeparatator);
    return "/" + segments[segments.length - 1] + "/";
}
exports.getPublicPath = getPublicPath;

/*
 * Gets the filename from an array.
 * */
export function getFileName(relativePaths: Dictionary<string>, chunkData: any) {
    return (relativePaths[chunkData.chunk.name] || OutputDirectoryDefault) + JsPlaceholder;
}
