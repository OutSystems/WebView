//using System;
//using CefSharp.ModelBinding;

//namespace WebViewControl {

//    internal class LambdaMethodBinder : IBinder {

//        private Func<object, Type, object> Bind { get; }

//        public LambdaMethodBinder(Func<object, Type, object> bind) {
//            Bind = bind;
//        }

//        object IBinder.Bind(object obj, Type modelType) {
//            return Bind(obj, modelType);
//        }
//    }
//}
