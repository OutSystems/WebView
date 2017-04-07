import { ITsType } from "./ts-types";
export interface ITsAnnotationArgument {
    name: string;
    value: string;
}
export interface ITsAnnotation {
    name: string;
    args: ITsAnnotationArgument[];
}
export interface ITsUnit {
    annotations: ITsAnnotation[];
    addAnnotation(annot: ITsAnnotation): any;
}
export interface ITopLevelTsUnit extends ITsUnit {
    name: string;
    functions: TsFunction[];
    interfaces: TsInterface[];
    classes: TsClass[];
    modules: TsModule[];
    enums: TsEnum[];
    addModule(unit: TsModule): any;
    addFunction(unit: TsFunction): any;
    addClass(unit: TsClass): any;
    addInterface(unit: TsInterface): any;
    addEnum(unit: TsEnum): any;
}
export declare abstract class AbstractTsUnit implements ITsUnit {
    name: string;
    annotations: ITsAnnotation[];
    constructor(name: string);
    addAnnotation(annot: ITsAnnotation): void;
}
export declare abstract class TopLevelTsUnit extends AbstractTsUnit implements ITopLevelTsUnit {
    functions: TsFunction[];
    interfaces: TsInterface[];
    classes: TsClass[];
    modules: TsModule[];
    enums: TsEnum[];
    constructor(name: string);
    addFunction(unit: TsFunction): void;
    addInterface(unit: TsInterface): void;
    addClass(unit: TsClass): void;
    addModule(unit: TsModule): void;
    addEnum(unit: TsEnum): void;
}
export declare class TsParameter {
    name: string;
    type: ITsType;
    constructor(name: string, type: ITsType);
}
export declare class TsEnumOption {
    name: string;
    id: number;
    constructor(name: string, id: number);
}
export declare class TsFunction extends AbstractTsUnit {
    name: string;
    parameters: TsParameter[];
    returnType: ITsType;
    constructor(name: string, parameters: TsParameter[], returnType: ITsType);
}
export declare class TsEnum extends AbstractTsUnit {
    name: string;
    options: TsEnumOption[];
    constructor(name: string, options: TsEnumOption[]);
}
export declare class TsInterface extends TopLevelTsUnit {
    $interface: void;
}
export declare class TsModule extends TopLevelTsUnit {
    $module: void;
}
export declare class TsClass extends TopLevelTsUnit {
    $class: void;
}
