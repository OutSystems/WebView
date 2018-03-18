import * as Types from "@outsystems/ts2lang/ts-types";
import * as Units from "@outsystems/ts2lang/ts-units";

const DelegateSuffix = "Delegate";
const ComponentAliasName = "Component";
const BaseComponentAliasName = "Base" + ComponentAliasName;
const ViewModuleClassName = "ViewModule";
const PropertiesClassName = "Properties";

function f(input: string) {
    return (input || "").replace(/\n/g, "\n    ");
}

function toPascalCase(name: string) {
    return name[0].toUpperCase() + name.substr(1);
}

function getFunctionReturnType(func: Units.TsFunction): string {
    if ((<Types.TsIdentifierType>func.returnType).parameters) {
        return getTypeName((<Types.TsIdentifierType>func.returnType).parameters[0]);
    }
    return getTypeName(func.returnType);
}

function getTypeName(tsType: Types.ITsType): string {
    switch (tsType.name) {
        case "string":
            return "string";
        case "number":
            return "int";
        case "boolean":
            return "bool";
        case "void":
            return "void";
        default:
            return tsType.name;
    }
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
    if (func.returnType != Types.TsVoidType) {
        throw new Error("Behavior method " + func.name + " return type must be void. Behavior methods cannot return values.");
    }
    return (
        `public ${generateMethodSignature(func)} {\n` +
        `    ExecuteMethodOnRoot("${func.name}"${func.parameters.map(p => ", " + p.name).join()});\n` +
        `}`
    );
}

function generateNativeApi(propsInterface: Units.TsInterface | null) {
    return f(
        `internal interface I${PropertiesClassName} {\n` +
        `    ${propsInterface ? propsInterface.functions.map(f => generateMethodSignature(f) + ";\n").join("") : ""}` + 
        `}\n` +
        `\n` +
        `private class ${PropertiesClassName} : I${PropertiesClassName} {\n` +
        `    protected readonly ${ComponentAliasName} owner;\n` +
        `    public ${PropertiesClassName}(${ComponentAliasName} owner) {\n` +
        `        this.owner = owner;\n` +
        `    }\n` +
        `    ${f(propsInterface ? (propsInterface.functions.length > 0 ? propsInterface.functions.map(f => generateNativeApiMethod(f)).join("\n") : "// the interface does not contain methods") : "")}\n` +
        `}`
    );
}

function generateNativeApiMethod(func: Units.TsFunction): string {
    let isVoid = func.returnType.name === Types.TsVoidType.name;
    return (
        `public ${generateMethodSignature(func)} {\n` +
        `    ${isVoid ? "" : "return "}owner.${toPascalCase(func.name)}?.Invoke(${func.parameters.map(p => p.name).join(", ")})${isVoid ? "" : ` ?? default(${getFunctionReturnType(func)})`};\n` +
        `}`
    );
}

function generateNativeApiObjects(objsInterfaces: Units.TsInterface[], enums: Units.TsEnum[]) {
    return f(
        objsInterfaces.map(generateNativeApiObject).join("\n") +
        "\n\n" +
        enums.map(generateNativeApiEnum).join("\n")
    );
}

function generateNativeApiObject(objInterface: Units.TsInterface) {
    return f(
        `public struct ${objInterface.name} {\n` +
        `${objInterface.properties.map(p => `public ${getTypeName(p.type)} ${p.name};`).join("\n")}\n` +
        `}`
    );
}

function generateNativeApiEnum(enumerate: Units.TsEnum) {
    return f(
        `public enum ${enumerate.name} {\n` +
        `${enumerate.options.map(o => `${o.name}`).join(",\n")}\n` +
        `}`
    );
}

function generateComponentBody(propsInterface: Units.TsInterface | null, behaviorsInterface: Units.TsInterface | null) {
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
    const PropertiesInterfaceSuffix = "Properties";

    let componentClass = module.classes[0];
    
    let propsInterface = module.interfaces.find((ifc) => ifc.name.startsWith("I") && ifc.name.endsWith(PropertiesInterfaceSuffix)) || null;
    let behaviorsInterface = module.interfaces.find((ifc) => ifc.name.startsWith("I") && ifc.name.endsWith("Behaviors")) || null;
    let objects = module.interfaces.filter(ifc => ifc !== propsInterface && ifc !== behaviorsInterface);
    let enums = module.enums;

    let fullPath = normalizePath(context["$fullpath"] as string);
    let path = normalizePath(context["$path"] as string);

    let namespace = context["namespace"];

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
    path = "/" + namespace + "/" + path.substr(0, fileExtensionIdx) + ".js";

    // set the output
    let filename = fullPathParts[fullPathParts.length - 1].slice(0, -fileExtensionLen);

    let output = normalizePath(context["$output"]);
    output += (output.charAt(output.length - 1) === "/" ? "" : "/") + filename + ".Generated.cs";
    context["$output"] = output;

    if (!componentClass) {
        return "";
    }

    let propsInterfaceCoreName = propsInterface ? propsInterface.name.substring(1, propsInterface.name.length - PropertiesInterfaceSuffix.length) : "";

    return (
        `/*** Auto-generated ***/

namespace ${namespace} {

    using ${ComponentAliasName} = ${componentClass.name};
    using ${BaseComponentAliasName} = ${context["baseComponentClass"] || "WebViewControl.ReactView"};

    public class ${componentClass.name} : ${BaseComponentAliasName} {

        ${f(generateNativeApiObjects(objects, enums))}

        ${f(generateNativeApi(propsInterface))}

        ${f(generateComponentBody(propsInterface, behaviorsInterface))} 

        protected override string JavascriptSource => \"${path}\";
        protected override string JavascriptName => \"${propsInterfaceCoreName}\";

        protected override object CreateNativeObject() {
            return new ${PropertiesClassName}(this);
        }
    }
}`);
}