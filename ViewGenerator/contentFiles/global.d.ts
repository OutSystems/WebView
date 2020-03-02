/// images support
declare module "*.jpg" {
    const value: any;
    export = value;
}
declare module "*.png" {
    const value: any;
    export = value;
}
declare module "*.jpeg" {
    const value: any;
    export = value;
}
declare module "*.bmp" {
    const value: any;
    export = value;
}
declare module "*.gif" {
    const value: any;
    export = value;
}
declare module "*.woff" {
    const value: any;
    export = value;
}
declare module "*.woff2" {
    const value: any;
    export = value;
}
declare module "*.ico" {
    const value: any;
    export = value;
}
declare module "*.svg" {
    const value: any;
    export = value;
}
declare module "*.html" {
    const value: any;
    export = value;
}

declare module "PluginsProvider" {
    export interface Type<T> extends Function { new(...args: any[]): T; }

    export interface IPluginsContext {
        getPluginInstance<T>(_class: Type<T>): T;
    }

    export const PluginsContext: React.Context<IPluginsContext>;
}

declare module "ResourceLoader" {
    export type ResourceLoaderUrlFormatter = (resourceKey: string, ...params: string[]) => string;

    export const ResourceLoader: React.Context<ResourceLoaderUrlFormatter>;
}

declare module "ViewFrame" {
    export interface IViewFrameProps<T> {
        name: keyof T;
        className?: string;
    }

    export class ViewFrame<T> extends React.Component<IViewFrameProps<T>> { }
}