
interface INativeApi {
    getItems(): Promise<IJsObj[]>;
    // TODO replace track code with actual object
    executeOnItem(trackCode: TrackCode<IJsObj>, value: string): void;
}

interface IWebApi {
    setItems(obj: IJsObj): void;
}

interface TrackCode<T> {
    obj?: T;
}

interface IJsObj {
    TrackCode: TrackCode<IJsObj>;
    Value: string;
}

