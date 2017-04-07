export interface ITsType {
    isBasic: boolean;
    name: string;
}
export declare class TsAnyType implements ITsType {
    isBasic: boolean;
    name: string;
}
export declare class TsArrayType implements ITsType {
    private inner;
    constructor(inner: ITsType);
    isBasic: boolean;
    name: string;
    getInner(): ITsType;
}
export declare class TsBasicType implements ITsType {
    private inner;
    private innerName;
    constructor(name: string);
    isBasic: boolean;
    name: string;
}
export declare const TsBooleanType: TsBasicType;
export declare const TsNumberType: TsBasicType;
export declare const TsStringType: TsBasicType;
export declare const TsVoidType: TsBasicType;
export declare class TsFunctionType implements ITsType {
    private retType;
    private args;
    constructor(args: Array<{
        name: string;
        type: ITsType;
    }>, ret: ITsType);
    isBasic: boolean;
    name: string;
}
export declare class TsIdentifierType implements ITsType {
    private originalName;
    private typeParameters;
    private _name;
    constructor(name: string, typeParameters?: Array<ITsType>);
    isBasic: boolean;
    name: string;
    parameters: ITsType[];
}
export declare class TsUnionType implements ITsType {
    private innerTypes;
    constructor(innerTypes: ITsType[]);
    isBasic: boolean;
    name: string;
}
