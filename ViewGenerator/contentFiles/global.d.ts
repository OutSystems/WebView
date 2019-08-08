/// built-in web-components
declare namespace JSX {
    interface IntrinsicElements {
        'view-frame': { id: string };
    }
}

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