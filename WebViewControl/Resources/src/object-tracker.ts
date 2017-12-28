declare var NativeApi: any;

const TrackCodeField = "TrackCode";
const ObjectsCache = new Map<number, object>();

// wrap native api to send only track codes to .net
export function wrapNativeApi(api: object) {
    for (var member in api) {
        if (api.hasOwnProperty(member) && api[member] instanceof Function) {
            api[member] = new Proxy(api[member], {
                apply: function (target, thisArg, argumentsList) {
                    if (argumentsList.length > 0) {
                        let trackCode: number = argumentsList[0][TrackCodeField];
                        if (trackCode !== undefined) {
                            argumentsList[0] = trackCode;
                        }
                    }

                    return trackObject(target.apply(thisArg, argumentsList));
                }
            });
        }
    }
}

function trackObject(obj: object): object {
    if (obj) {
        let trackCode = obj[TrackCodeField];
        if (trackCode) {
            ObjectsCache.set(trackCode, obj);
        }

    }
    return obj;
}

if (typeof NativeApi !== "undefined") {
    wrapNativeApi(NativeApi);
}