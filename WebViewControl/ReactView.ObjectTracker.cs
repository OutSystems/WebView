using System;
using System.Collections;
using System.Collections.Generic;

namespace WebViewControl {
    partial class ReactView {

        private readonly Dictionary<long, object> jsObjects = new Dictionary<long, object>();

        public struct TrackCode {
            public long Value;
        }

        public class ViewObject {
            public TrackCode TrackCode;
        }

        private object GetTrackedObject(long id) {
            object obj;
            if (jsObjects.TryGetValue(id, out obj)) {
                return obj;
            }
            throw new InvalidOperationException("Unknown object with track code:");
        }

        private object ResolveObject(object obj, Type modelType) {
            if (modelType.IsInterface) {
                // TODO check if its a ViewObject
                // int .. might not be enough
                // trackcode
                var trackCode = (int)((Dictionary<string, object>)obj)["Value"];
                return GetTrackedObject(trackCode);
            }
            return defaultBinder.Bind(obj, modelType);
        }

        private object InjectTracker(Func<object> originalMethod) {
            object result = originalMethod();
            if (result != null) {
                if (result is IEnumerable) {
                    var objects = (IEnumerable)result;
                    foreach (object item in objects) {
                        TrackObject(item);
                    }
                } else {
                    TrackObject(result);
                }
            }
            return result;
        }

        private void TrackObject(object obj) {
            var viewObj = obj as ViewObject;
            if (viewObj == null) {
                throw new InvalidOperationException("Object is not a view object");
            }

            if (viewObj.TrackCode.Value == 0) {
                viewObj.TrackCode.Value = ++objectCounter;
                jsObjects[viewObj.TrackCode.Value] = obj;
            }
        }
    }
}
