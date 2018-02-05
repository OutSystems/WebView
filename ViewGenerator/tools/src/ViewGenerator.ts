import * as Types from "@outsystems/ts2lang/ts-types";
import * as Units from "@outsystems/ts2lang/ts-units";

const DelegateSuffix = "Delegate";

function f(input: string) {
    return (input || "").replace(/\n/g, "\n    ");
}

function toPascalCase(name: string) {
    return name[0].toUpperCase() + name.substr(1);
}

function getFunctionReturnType(func: Units.TsFunction): string {
    if ((<Types.TsIdentifierType>func.returnType).parameters) {
        return (<Types.TsIdentifierType>func.returnType).parameters[0].name;
    }
    return func.returnType.name;
}

function getTypeName(tsType: Types.ITsType): string {
    return tsType.name;
}

function generateMethodSignature(func: Units.TsFunction, functionSuffix: string = "") {
    return `${getFunctionReturnType(func)} ${toPascalCase(func.name)}${functionSuffix}(${func.parameters.map(p => getTypeName(p.type) + " " + p.name).join(", ")})`;
}

function generateProperty(func: Units.TsFunction): string {
    return (
        `public delegate ${generateMethodSignature(func, DelegateSuffix)};\n` +
        `public event ${toPascalCase(func.name)}${DelegateSuffix} ${toPascalCase(func.name)};`
    );
}

function generateBehaviorMethod(func: Units.TsFunction): string {
    return (
        `public ${generateMethodSignature(func)} {\n` +
        `    ExecuteMethodOnRoot("${func.name}"${func.parameters.map(p => ", " + p.name).join()});\n` +
        `}`
    );
}

function generateNativeApi(propsInterface: Units.TsInterface | null) {
    return f(
        `private class NativeApi : BaseNativeApi<ControlType> {\n` +
        `    public NativeApi(ControlType owner) : base(owner) { }\n` +
        `    ${f(propsInterface ? propsInterface.functions.map(f => generateNativeApiMethod(f)).join("\n") : "")}\n` +
        `}`
    );
}

function generateNativeApiMethod(func: Units.TsFunction): string {
    let isVoid = func.returnType.name === Types.TsVoidType.name;
    return (
        `public ${generateMethodSignature(func)} {\n` +
        `    ${isVoid ? "" : "return "}owner.${toPascalCase(func.name)}?.Invoke(${func.parameters.map(p => p.name).join(", ")});\n` +
        `}`
    );
}

function generateNativeApiObjects(objsInterfaces: Units.TsInterface[]) {
    return objsInterfaces.map(generateNativeApiObject).join("\n");
}

function generateNativeApiObject(objInterface: Units.TsInterface) {
    return f(
        `public struct ${objInterface.name} {\n` +
        `    ${objInterface.properties.map(p => `public ${getTypeName(p.type)} ${toPascalCase(p.name)};`).join("\n")}\n` +
        `}`
    );
}

function generateControlBody(propsInterface: Units.TsInterface | null, behaviorsInterface: Units.TsInterface | null) {
    return f(
        (propsInterface ? propsInterface.functions.map(f => generateProperty(f)).join("\n") : "") +
        "\n" +
        (behaviorsInterface ? behaviorsInterface.functions.map(f => generateBehaviorMethod(f)).join("\n") : "")
    );
}

function normalizePath(path: string): string {
    return path.replace(/\\/g, "/")
}

export function transform(module: Units.TsModule, context: Object): string {
    let componentClass = module.classes[0];
    let propsInterface = module.interfaces.find((ifc) => ifc.name.startsWith("I") && ifc.name.endsWith("Properties")) || null;
    let behaviorsInterface = module.interfaces.find((ifc) => ifc.name.startsWith("I") && ifc.name.endsWith("Behaviors")) || null;
    let objects = module.interfaces.filter(ifc => ifc !== propsInterface && ifc !== behaviorsInterface);

    let fullPath = normalizePath(context["$fullpath"] as string);
    let path = normalizePath(context["$path"] as string);

    let pathDepth = path.split("/").length;
    let fullPathParts = fullPath.split("/");

    if (pathDepth > 0) {
        fullPathParts[fullPathParts.length - 2] = context["javascriptDistPath"] || "dist"; // replace
        // take out the common part of fullpath and path
        path = fullPathParts.slice(-pathDepth).join("/");
    }

    // replace file extension with .js
    let fileExtensionIdx = path.lastIndexOf(".");
    let fileExtensionLen = path.length - fileExtensionIdx;
    path = path.substr(0, fileExtensionIdx) + ".js";

    // set the output
    let filename = fullPathParts[fullPathParts.length - 1].slice(0, -fileExtensionLen);

    let output = normalizePath(context["$output"]);
    output += (output.charAt(output.length - 1) === "/" ? "" : "/") + filename + ".Generated.cs";
    context["$output"] = output;

    if (!componentClass) {
        return "";
    }

    return (
        `/*** Auto-generated ***/

namespace ${context["namespace"]} {

    using ControlType = ${componentClass.name};

    public class ${componentClass.name} : ${context["baseComponentClass"] || "WebViewControl.ReactView"} {

        ${f(generateNativeApiObjects(objects))}

        ${f(generateNativeApi(propsInterface))}

        ${f(generateControlBody(propsInterface, behaviorsInterface))} 

        protected override string Source => "${path}";

        protected override object CreateRootPropertiesObject() {
            return new NativeApi(this);
        }
    }
}`);
}