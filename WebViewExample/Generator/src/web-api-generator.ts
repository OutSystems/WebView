/// <reference path="../typings/index.d.ts"/>
/// <reference path="../types/ts-types.d.ts"/>
/// <reference path="../types/ts-units.d.ts"/>

import * as Types from "../types/ts-types";
import * as Units from "../types/ts-units";

function toPascalCase(name: string) {
    return name[0].toUpperCase() + name.substr(1);
}

function getFunctionReturnType(func: Units.TsFunction): string {
    if ((<Types.TsIdentifierType> func.returnType).parameters) {
        return (<Types.TsIdentifierType>func.returnType).parameters[0].name;
    }
    return func.returnType.name;
}

function getTypeName(tsType: Types.ITsType): string {
    console.log(tsType.name);
    if (tsType.name === "TrackCode") {
        return (<Types.TsIdentifierType>tsType).parameters[0].name;
    }
    return tsType.name;
}

function generateMethod(func: Units.TsFunction) {
    return (
        `        public ${getFunctionReturnType(func)} ${toPascalCase(func.name)}(${func.parameters.map(p => getTypeName(p.type) + " " + p.name).join(", ")}) {\n` +
        `            ExecuteJavascript("${func}");\n` +
        `        }`
    );
}

function generateClass(tsInterface: Units.TsInterface) {
    let methods = tsInterface.functions.map(f => generateMethod(f)).join("\n");
    return (
`
    class ${tsInterface.name} : WebViewControl.ReactView.WebApi {
${methods} 
    }
`);
}

export function transform(module: Units.TsModule, context: Object): string {
    let classes = module.interfaces.map(i => generateClass(i)).join("\n");
    return (
`using System;

namespace ${context["namespace"]} {
${classes}
}`);
}