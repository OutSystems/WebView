declare global {
    namespace JSX {
        interface IntrinsicElements {
            'view-frame': ViewFrameAttributes;
        }

        interface ViewFrameAttributes {
            name: string;
        }
    }
}