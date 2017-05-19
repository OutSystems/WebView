using System;
using System.Collections;

namespace WebViewControl {

    internal class JsObjectInterceptor /* TODO : IInterceptor */ {

        private readonly ReactView reactView;

        public JsObjectInterceptor(ReactView reactView) {
            this.reactView = reactView;
        }

        public object Intercept(Func<object> originalMethod, string methodName) {
            object result = originalMethod();
            if (result != null) {
                if (result is IEnumerable) {
                    var objects = (IEnumerable)result;
                    foreach (object item in objects) {
                        reactView.TrackObject(item);
                    }
                } else {
                    reactView.TrackObject(result);
                }
            }
            return result;
        }
    }
}
