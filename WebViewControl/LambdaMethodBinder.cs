using System;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class LambdaMethodBinder : IBinder {

        private readonly Func<object, Type, object> bind;

        public LambdaMethodBinder(Func<object, Type, object> bind) {
            this.bind = bind;
        }

        object IBinder.Bind(object obj, Type modelType) {
            return bind(obj, modelType);
        }
    }
}
