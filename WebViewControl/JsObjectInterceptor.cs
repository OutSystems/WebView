using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class JsObjectInterceptor : IInterceptor {

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
