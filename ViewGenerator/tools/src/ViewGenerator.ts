import * as Types from "@outsystems/ts2lang/ts-types";
import * as Units from "@outsystems/ts2lang/ts-units";

const GeneratedFilesHeader = "/*** Auto-generated ***/";

const DelegateSuffix = "EventHandler";
const ComponentAliasName = "Component";
const BaseComponentAliasName = "Base" + ComponentAliasName;
const PropertiesClassName = "Properties";
const PropertiesInterfaceSuffix = "Properties";
const BehaviorsInterfaceSuffix = "Behaviors";

function f(input: string) {
    if (!input) {
        return "";
    }
    return input.replace(/\n/g, "\n    ");
}

function toPascalCase(name: string) {
    return name[0].toUpperCase() + name.substr(1);
}

function normalizePath(path: string): string {
    return path.replace(/\\/g, "/")
}

class Generator {

    private propsInterface: Units.TsInterface | null;
    private behaviorsInterface: Units.TsInterface | null;
    private component: Units.TsClass;
    private objects: Units.TsInterface[];
    private enums: Units.TsEnum[];
    private propsInterfaceCoreName: string;
    private componentName: string;

    constructor(
        module: Units.TsModule,
        private namespace: string,
        private relativePath: string,
        private fullPath: string,
        private filename: string,
        private preamble: string,
        private baseComponentClass: string,
        private baseComponentInterface: string) {

        this.component = module.classes.filter(c => c.isPublic)[0];
        let interfaces = module.interfaces.filter((ifc) => ifc.isPublic && ifc.name.startsWith("I"));
        this.propsInterface = interfaces.find((ifc) => ifc.name.endsWith(PropertiesInterfaceSuffix)) || null;
        this.behaviorsInterface = interfaces.find((ifc) => ifc.name.endsWith(BehaviorsInterfaceSuffix)) || null;
        this.objects = module.interfaces.filter(ifc => ifc.isPublic && ifc !== this.propsInterface && ifc !== this.behaviorsInterface);
        this.enums = module.enums.filter(e => e.isPublic);

        this.propsInterfaceCoreName = this.propsInterface ? this.propsInterface.name.substring(1, this.propsInterface.name.length - PropertiesInterfaceSuffix.length) : "";
        this.componentName = this.component ? this.component.name : this.propsInterfaceCoreName;
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

                if (tsType.isBasic) {
                    return tsType.name;
                }

                return this.getTypeNameAlias(tsType.name);
        }
    }

    private getTypeNameAlias(name: string): string {
        if (name.startsWith("I") && name.charAt(1) === name.charAt(1).toUpperCase()) {
            return name.substr(1);
        }
        return name;
    }

    private generateMethodSignature(func: Units.TsFunction, functionPrefix: string = "", functionSuffix: string = "") {
        return `${this.getFunctionReturnType(func)} ${functionPrefix}${toPascalCase(func.name)}${functionSuffix}(${func.parameters.map(p => this.getTypeName(p.type) + " " + p.name).join(", ")})`;
    }

    private generateNativeApi() {
        return (
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
        return this.objects.map(o => this.generateNativeApiObject(o))
            .concat(this.enums.map(e => this.generateNativeApiEnum(e)))
            .join("\n\n");
    }

    private generateNativeApiObject(objInterface: Units.TsInterface) {
        return (
            `public struct ${this.getTypeNameAlias(objInterface.name)} {\n` +
            `    ${f(objInterface.properties.map(p => `public ${this.getTypeName(p.type)} ${p.name} { get; set; }`).join("\n"))}\n` +
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

    private generatePropertyDelegate(func: Units.TsFunction, accessibility: string = "public"): string {
        accessibility = accessibility ? accessibility + " " : "";
        return `${accessibility}delegate ${this.generateMethodSignature(func, this.componentName, DelegateSuffix)}`;
    }

    private generatePropertyEvent(func: Units.TsFunction, accessibility: string = "public"): string {
        accessibility = accessibility ? accessibility + " " : "";
        return `${accessibility}event ${this.componentName}${toPascalCase(func.name)}${DelegateSuffix} ${toPascalCase(func.name)}`
    }

    private generateComponentBody(generatePropertyEvent: (prop: Units.TsFunction) => string, generateBehaviorMethod: (func: Units.TsFunction) => string) {
        return (this.propsInterface ? this.propsInterface.functions.map(f => generatePropertyEvent(f)) : [])
            .concat(this.behaviorsInterface ? this.behaviorsInterface.functions.map(f => generateBehaviorMethod(f)) : [])
            .join("\n");
    }

    private generateComponentAdapter() {
        const ComponentProperty = "Component";

        const generateProperty = (func: Units.TsFunction) => {
            let eventName = toPascalCase(func.name);
            return (
                `public event ${this.componentName}${eventName}${DelegateSuffix} ${eventName} {\n` +
                `    add { ${ComponentProperty}.${eventName} += value; }\n` +
                `    remove { ${ComponentProperty}.${eventName} -= value; }\n` +
                `}`
            );
        };
        const generateBehaviorMethod = (func: Units.TsFunction) => {
            let isVoid = func.returnType === Types.TsVoidType;

            return (
                `public ${this.generateMethodSignature(func)} {\n` +
                `    ${isVoid ? "" : "return "}${ComponentProperty}.${toPascalCase(func.name)}(${func.parameters.map(p => p.name).join(",")});\n` +
                `}`
            );
        };
        return (
            `partial class ${this.componentName}Adapter : I${this.componentName} {\n` +
            `\n` +
            `    ${f(this.generateComponentBody(generateProperty, generateBehaviorMethod))}\n` +
            `}\n`
        );
    }

    private generateComponentClass() {
        const generatePropertyEvent = (func: Units.TsFunction) => `${this.generatePropertyEvent(func)};`;

        const generateBehaviorMethod = (func: Units.TsFunction) => {
            let params = [`"${func.name}"`].concat(func.parameters.map(p => p.name)).join(", ");
            let body = "";
            if (func.returnType != Types.TsVoidType) {
                let returnType = this.getFunctionReturnType(func);
                body = `return ExecutionEngine.EvaluateMethod<${returnType}>(this, ${params});`;
            } else {
                body = `ExecutionEngine.ExecuteMethod(this, ${params});`;
            }

            return (
                `public ${this.generateMethodSignature(func)} {\n` +
                `    ${body}\n` +
                `}`
            );
        };

        return (
            `public partial class ${this.componentName} : ${BaseComponentAliasName} {\n` +
            `    \n` +
            `    ${f(this.generateNativeApi())}\n` +
            `    \n` +
            `    ${f(this.generateComponentBody(generatePropertyEvent, generateBehaviorMethod))}\n` +
            `    \n` +
            `    protected override string JavascriptSource => \"${this.relativePath}\";\n` +
            `    protected override string NativeObjectName => \"${this.propsInterfaceCoreName}\";\n` +
            `    protected override string ModuleName => \"${this.filename}\";\n` +
            `    \n` +
            `    protected override object CreateNativeObject() {\n` +
            `        return new ${PropertiesClassName}(this);\n` +
            `    }\n` +
            `    \n` +
            `    #if DEBUG\n` +
            `    protected override string Source => \"${this.fullPath}\";\n` +
            `    #endif\n` +
            `}`
        );
    }

    private generateComponentInterface() {
        const generateProperty = (func: Units.TsFunction) => `${this.generatePropertyEvent(func, "")};`;
        const generateBehaviorMethod = (func: Units.TsFunction) => `${this.generateMethodSignature(func)};`;

        return (
            `public partial interface I${this.componentName} ${this.baseComponentInterface ? `: ${this.baseComponentInterface} ` : ""} {\n` +
            `    ${f(this.generateComponentBody(generateProperty, generateBehaviorMethod))}\n` +
            `}`
        );
    }

    public generateComponent(emitComponentClass: boolean, emitComponentInterface: boolean, emitComponentAdapter: boolean, emitViewObjects: boolean) {
        if (!((this.component && this.behaviorsInterface && this.behaviorsInterface.functions.length > 0) ||
            (this.propsInterface && this.propsInterface.functions.length > 0))) {
            return "";
        }

        const generateAliases = () => {
            return (
                `using ${ComponentAliasName} = ${this.componentName};\n` +
                `using ${BaseComponentAliasName} = ${this.baseComponentClass || "WebViewControl.ReactView"};`
            );
        };

        const generatePropertiesDelegates = () => this.propsInterface ? this.propsInterface.functions.map(f => `${this.generatePropertyDelegate(f)};`).join("\n") : "";

        return (
            `${GeneratedFilesHeader}\n` +
            `${this.preamble}\n` +
            `namespace ${this.namespace} {\n` +
            `\n` +
            (emitComponentClass ? `    ${f(generateAliases())}\n\n` : ``) +
            (emitComponentInterface ? `    ${f(generatePropertiesDelegates())}\n\n` : ``) +
            (emitViewObjects ? `    ${f(this.generateNativeApiObjects())}\n\n` : "") +
            (emitComponentInterface ? `    ${f(this.generateComponentInterface())}\n\n` : ``) +
            (emitComponentClass ? `    ${f(this.generateComponentClass())}\n\n` : ``) +
            (emitComponentAdapter ? `    ${f(this.generateComponentAdapter())}\n\n` : ``) +
            `}`
        );
    }
}

function combinePath(path: string, rest: string) {
    return path + (path.endsWith("/") ? "" : "/") + (rest.startsWith("/") ? rest.substr(1) : rest);
}

export function transform(module: Units.TsModule, context: Object): string {
    debugger; // left on purpose to ease debugging
    const JsExtension = ".js";
    let namespace = context["namespace"];
    let baseDir = normalizePath(context["$baseDir"]);
    let fullPath = normalizePath(context["$fullpath"]);
    let javascriptDistPath = normalizePath(context["javascriptDistPath"] || "");

    let fileExtensionLen = fullPath.length - fullPath.lastIndexOf(".");
    let filenameWithoutExtension = fullPath.slice(fullPath.lastIndexOf("/") + 1, -fileExtensionLen);

    let javascriptFullPath = fullPath.slice(0, -fileExtensionLen) + JsExtension; // replace the tsx/ts extension with js extension

    let javascriptRelativePath = javascriptFullPath.substr(baseDir.length + 1); // remove the base dir

    if (javascriptDistPath.endsWith(JsExtension)) {
        // dist path has extension... then its a complete filename, use as the output
        javascriptRelativePath = javascriptDistPath;
    } else {
        // else combine dist path with input filename
        javascriptRelativePath = combinePath(javascriptDistPath, javascriptRelativePath);
    }

    javascriptFullPath = combinePath(baseDir, javascriptRelativePath); // add the base dir
    javascriptRelativePath = "/" + combinePath(namespace, javascriptRelativePath); // add the namespace

    let output = normalizePath(context["$output"]);
    output = combinePath(output, filenameWithoutExtension + ".Generated.cs");
    context["$output"] = output;
    
    let generator = new Generator(
        module,
        namespace,
        javascriptRelativePath,
        javascriptFullPath,
        filenameWithoutExtension,
        context["preamble"] || "",
        context["baseComponentClass"],
        context["baseComponentInterface"],
    );

    let emitComponent = context["emitComponent"] !== false;
    let emitComponentInterface = context["emitComponentInterface"] !== false;
    let emitComponentAdapter = context["emitComponentAdapter"] === true;
    let emitViewObjects = context["emitViewObjects"] !== false;

    return generator.generateComponent(emitComponent, emitComponentInterface, emitComponentAdapter, emitViewObjects);
}