import * as Types from "@outsystems/ts2lang/ts-types";
import * as Units from "@outsystems/ts2lang/ts-units";

const GeneratedFilesHeader = "/*** Auto-generated ***/";

const DelegateSuffix = "Delegate";
const ComponentAliasName = "Component";
const BaseComponentAliasName = "Base" + ComponentAliasName;
const ViewModuleClassName = "ViewModule";
const PropertiesClassName = "Properties";
const PropertiesInterfaceSuffix = "Properties";
const BehaviorsInterfaceSuffix = "Behaviors";

function f(input: string) {
    return (input || "").replace(/\n/g, "\n    ");
}

function toPascalCase(name: string) {
    return name[0].toUpperCase() + name.substr(1);
}

function normalizePath(path: string): string {
    return path.replace(/\\/g, "/")
}

class Generator {

    private aliases: { [name: string]: string } = {};
    private propsInterface: Units.TsInterface | null;
    private behaviorsInterface: Units.TsInterface | null;
    private component: Units.TsClass;
    private objects: Units.TsInterface[];
    private enums: Units.TsEnum[];

    constructor(
        module: Units.TsModule,
        private namespace: string,
        private relativePath: string,
        private fullPath: string,
        private preamble: string,
        private baseComponentClass: string) {

        this.component = module.classes.filter(c => c.isPublic)[0];
        let interfaces = module.interfaces.filter((ifc) => ifc.isPublic && ifc.name.startsWith("I"));
        this.propsInterface = interfaces.find((ifc) => ifc.name.endsWith(PropertiesInterfaceSuffix)) || null;
        this.behaviorsInterface = interfaces.find((ifc) => ifc.name.endsWith(BehaviorsInterfaceSuffix)) || null;
        this.objects = module.interfaces.filter(ifc => ifc.isPublic && ifc !== this.propsInterface && ifc !== this.behaviorsInterface);
        this.enums = module.enums.filter(e => e.isPublic);

        this.objects.filter(o => o.name.startsWith("I")).forEach(o => this.aliases[o.name] = o.name.substr(1));
    }

    private getFunctionReturnType(func: Units.TsFunction): string {
        let returnType = <Types.TsIdentifierType>func.returnType;
        if (returnType.parameters && returnType.parameters.length > 0) {
            return this.getTypeName(returnType.parameters[0]);
        }
        return this.getTypeName(returnType);
    }

    private getTypeName(tsType: Types.ITsType): string {
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
                if (tsType instanceof Types.TsArrayType) {
                    return this.getTypeName(tsType.getInner()) + "[]";
                }
                return this.aliases[tsType.name] || tsType.name;
        }
    }

    private generateMethodSignature(func: Units.TsFunction, functionSuffix: string = "") {
        return `${this.getFunctionReturnType(func)} ${toPascalCase(func.name)}${functionSuffix}(${func.parameters.map(p => this.getTypeName(p.type) + " " + p.name).join(", ")})`;
    }

    private generateProperty(func: Units.TsFunction): string {
        return (
            `public delegate ${this.generateMethodSignature(func, DelegateSuffix)};\n` +
            `public event ${toPascalCase(func.name)}${DelegateSuffix} ${toPascalCase(func.name)};`
        );
    }

    private generateBehaviorMethod(func: Units.TsFunction): string {
        if (func.returnType != Types.TsVoidType) {
            let returnType = this.getFunctionReturnType(func);
            return (
                `public ${this.generateMethodSignature(func)} {\n` +
                `    return ExecutionEngine.EvaluateMethod<${returnType}>(this, "${func.name}"${func.parameters.map(p => ", " + p.name).join()});\n` +
                `}`
            );
        }
        return (
            `public ${this.generateMethodSignature(func)} {\n` +
            `    ExecutionEngine.ExecuteMethod(this, "${func.name}"${func.parameters.map(p => ", " + p.name).join()});\n` +
            `}`
        );
    }

    private generateNativeApi() {
        return f(
            `internal interface I${PropertiesClassName} {\n` +
            `    ${f(this.propsInterface ? this.propsInterface.functions.map(f => this.generateMethodSignature(f) + ";").join("\n") : "")}\n` + 
            `}\n` +
            `\n` +
            `private class ${PropertiesClassName} : I${PropertiesClassName} {\n` +
            `    protected readonly ${ComponentAliasName} owner;\n` +
            `    public ${PropertiesClassName}(${ComponentAliasName} owner) {\n` +
            `        this.owner = owner;\n` +
            `    }\n` +
            `    ${f(this.propsInterface ? (this.propsInterface.functions.length > 0 ? this.propsInterface.functions.map(f => this.generateNativeApiMethod(f)).join("\n") : "// the interface does not contain methods") : "")}\n` +
            `}`
        );
    }

    private generateNativeApiMethod(func: Units.TsFunction): string {
        let isVoid = func.returnType.name === Types.TsVoidType.name;
        return (
            `public ${this.generateMethodSignature(func)} {\n` +
            `    ${isVoid ? "" : "return "}owner.${toPascalCase(func.name)}?.Invoke(${func.parameters.map(p => p.name).join(", ")})${isVoid ? "" : ` ?? default(${this.getFunctionReturnType(func)})`};\n` +
            `}`
        );
    }

    private generateNativeApiObjects() {
        return f(
            this.objects.map(o => this.generateNativeApiObject(o)).join("\n") +
            "\n\n" +
            this.enums.map(e => this.generateNativeApiEnum(e)).join("\n")
        );
    }

    private generateNativeApiObject(objInterface: Units.TsInterface) {
        return (
            `public struct ${this.aliases[objInterface.name] || objInterface.name} {\n` +
            `    ${f(objInterface.properties.map(p => `public ${this.getTypeName(p.type)} ${p.name};`).join("\n"))}\n` +
            `}`
        );
    }

    private generateNativeApiEnum(enumerate: Units.TsEnum) {
        return (
            `public enum ${enumerate.name} {\n` +
            `    ${f(enumerate.options.map(o => `${o.name}`).join(",\n"))}\n` +
            `}`
        );
    }

    private generateComponentBody() {
        return f(
            (this.propsInterface ? this.propsInterface.functions.map(f => this.generateProperty(f)).join("\n") : "") +
            "\n" +
            (this.behaviorsInterface ? this.behaviorsInterface.functions.map(f => this.generateBehaviorMethod(f)).join("\n") : "")
        );
    }

    public generateComponent(emitObjects: boolean) {
        if (!this.component) {
            return "";
        }

        let propsInterfaceCoreName = this.propsInterface ? this.propsInterface.name.substring(1, this.propsInterface.name.length - PropertiesInterfaceSuffix.length) : "";

        return (
            `${GeneratedFilesHeader}\n` +
            `${this.preamble}\n` +
            `namespace ${this.namespace} {\n` +
            `\n` +
            `    using ${ComponentAliasName} = ${this.component.name};\n` +
            `    using ${BaseComponentAliasName} = ${this.baseComponentClass || "WebViewControl.ReactView"};\n` +
            `\n` +
            `    ${emitObjects ? (this.generateNativeApiObjects() + "\n") : ""}` +
            `\n` +
            `    public class ${this.component.name} : ${BaseComponentAliasName} {\n` +
            `\n` +
            `        ${f(this.generateNativeApi())}\n` +
            `\n` +
            `        ${f(this.generateComponentBody())}\n` +
            `\n` +
            `        protected override string JavascriptSource => \"${this.relativePath}\";\n` +
            `        protected override string NativeObjectName => \"${propsInterfaceCoreName}\";\n` +
            `\n` +
            `        protected override object CreateNativeObject() {\n` +
            `            return new ${PropertiesClassName}(this);\n` +
            `        }\n` +
            `\n` +
            `#if DEBUG\n` +
            `        protected override string Source => \"${this.fullPath}\";\n` +
            `#endif\n` +
            `    }\n` +
            `}`
        );
    }

    public generateObjects() {
        return (
            `${GeneratedFilesHeader}\n` +
            `${this.preamble}\n` +
            `namespace ${this.namespace} {\n` +
            `\n` +
            `    ${this.generateNativeApiObjects()}\n` +
            `\n` +
            `}`
        );
    }
}

function combinePath(path: string, rest: string) {
    return path + (path.endsWith("/") ? "" : "/") + (rest.startsWith("/") ? rest.substr(1) : rest);
}

export function transform(module: Units.TsModule, context: Object): string {
    let namespace = context["namespace"];
    let baseDir = normalizePath(context["$baseDir"]);
    let fullPath = normalizePath(context["$fullpath"]);
    let javascriptDistPath = normalizePath(context["javascriptDistPath"] || "") || "View/dist";
    let javascriptDistPathDepth = javascriptDistPath.split("/").filter(p => p !== "").length;

    let fileExtensionLen = fullPath.length - fullPath.lastIndexOf(".");
    let filenameWithoutExtension = fullPath.slice(fullPath.lastIndexOf("/") + 1, -fileExtensionLen);
    
    let javascriptFullPath = fullPath.slice(0, -fileExtensionLen) + ".js"; // replace the tsx/ts extension with js extension

    let javascriptRelativePath = javascriptFullPath.substr(baseDir.length + 1); // remove the base dir
    javascriptRelativePath = combinePath(javascriptDistPath, javascriptRelativePath.split("/").filter(p => p !== "").slice(javascriptDistPathDepth).join("/")); // replace the src dir with dist dir
    
    javascriptFullPath = combinePath(baseDir, javascriptRelativePath); // add the base dir
    javascriptRelativePath = "/" + combinePath(namespace, javascriptRelativePath); // add the namespace

    let output = normalizePath(context["$output"]);
    output = combinePath(output, filenameWithoutExtension + ".Generated.cs");
    context["$output"] = output;
    
    let generator = new Generator(module, namespace, javascriptRelativePath, javascriptFullPath, context["preamble"] || "", context["baseComponentClass"]);

    switch (context["emitViewObjects"]) {
        case "only": // emit only view objects
            return generator.generateObjects();
        case "none": // do not emit view objects
            return generator.generateComponent(false);
        default: // emit view objects in component class file
            return generator.generateComponent(true);
    }
}