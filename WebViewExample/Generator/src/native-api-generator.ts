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

function generateInterface(_interface: Units.TsInterface) {
    let methods = _interface.functions.map(f => `        ${getFunctionReturnType(f)} ${toPascalCase(f.name)}(${f.parameters.map(p => getTypeName(p.type) + " " + p.name).join(", ")});`).join("\n");
    return (
`
    interface ${_interface.name} {
${methods} 
    }
`);
}

export function transform(module: Units.TsModule, context: Object): string {
    let interfaces = module.interfaces.map(i => generateInterface(i)).join("\n");
    return (
`using System;

namespace ${context["namespace"]} {
${interfaces}
}`);
}